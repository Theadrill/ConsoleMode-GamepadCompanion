using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Engine.Services
{
    /// <summary>
    /// Serviço de geração e cache de fundos widescreen atmosféricos (Hero Blur & Vignette)
    /// a partir de capas verticais para a Biblioteca de Jogos.
    /// Pré-processa e salva em disco uma única vez para garantir zero impacto em runtime (60 FPS).
    /// </summary>
    public sealed class HeroBackgroundService
    {
        public const string HeroBlurSuffix = "_hero_blur.jpg";
        public const int HeroWidth = 960;
        public const int HeroHeight = 540;

        /// <summary>
        /// Obtém o caminho do arquivo de fundo correspondente a uma capa.
        /// </summary>
        public static string GetHeroBackgroundPath(string coverPath)
        {
            if (string.IsNullOrWhiteSpace(coverPath)) return string.Empty;

            string dir = Path.GetDirectoryName(coverPath) ?? string.Empty;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(coverPath);
            return Path.Combine(dir, fileNameWithoutExt + HeroBlurSuffix);
        }

        /// <summary>
        /// Garante que o fundo widescreen exista no disco para a capa especificada.
        /// Se já existir, retorna o caminho existente imediatamente sem reprocessar.
        /// </summary>
        public string EnsureHeroBackground(string coverPath)
        {
            if (string.IsNullOrWhiteSpace(coverPath) || !File.Exists(coverPath))
                return string.Empty;

            // Se for o próprio hero blur, não reprocessa
            if (coverPath.EndsWith(HeroBlurSuffix, StringComparison.OrdinalIgnoreCase))
                return coverPath;

            string heroPath = GetHeroBackgroundPath(coverPath);
            if (File.Exists(heroPath))
            {
                return heroPath;
            }

            try
            {
                GenerateHeroBackground(coverPath, heroPath);
                return File.Exists(heroPath) ? heroPath : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gera a imagem de fundo widescreen aplicando Center Crop 16:9, Downscale, Box Blur,
        /// e Vinheta Escura Atmosférica de 70% com degradê para leitura perfeita de cards.
        /// </summary>
        public void GenerateHeroBackground(string sourceCoverPath, string destinationHeroPath)
        {
            if (string.IsNullOrWhiteSpace(sourceCoverPath) || !File.Exists(sourceCoverPath))
                throw new FileNotFoundException("Capa de origem não encontrada.", sourceCoverPath);

            using (var stream = new FileStream(sourceCoverPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var srcImage = Image.FromStream(stream))
            {
                using (var heroBitmap = ProcessHeroImage(srcImage, HeroWidth, HeroHeight))
                {
                    string destDir = Path.GetDirectoryName(destinationHeroPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    SaveAsJpeg(heroBitmap, destinationHeroPath, 80);
                }
            }
        }

        /// <summary>
        /// Processa a imagem na memória gerando o bitmap widescreen atmosférico com blur e vinheta.
        /// </summary>
        public static Bitmap ProcessHeroImage(Image srcImage, int targetWidth, int targetHeight)
        {
            if (srcImage == null) throw new ArgumentNullException(nameof(srcImage));

            int srcW = srcImage.Width;
            int srcH = srcImage.Height;

            // 1. Center-Crop em aspect ratio widescreen 16:9 com leve bias superior
            // Em pôsteres de games, a arte principal/personagens costuma ficar do meio para cima.
            float targetRatio = (float)targetWidth / targetHeight;
            float srcRatio = (float)srcW / srcH;

            Rectangle cropRect;
            if (srcRatio > targetRatio)
            {
                int cropW = (int)(srcH * targetRatio);
                int cropX = (srcW - cropW) / 2;
                cropRect = new Rectangle(cropX, 0, cropW, srcH);
            }
            else
            {
                int cropH = (int)(srcW / targetRatio);
                int cropY = (int)((srcH - cropH) * 0.35f);
                cropY = Math.Max(0, Math.Min(srcH - cropH, cropY));
                cropRect = new Rectangle(0, cropY, srcW, cropH);
            }

            // 2. Extrai recorte em resolução moderada (480x270) para preservar silhuetas e detalhes reconhecíveis
            int blurW = 480;
            int blurH = 270;
            using (var smallBmp = new Bitmap(blurW, blurH, PixelFormat.Format32bppArgb))
            {
                using (var gSmall = Graphics.FromImage(smallBmp))
                {
                    gSmall.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gSmall.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    gSmall.DrawImage(srcImage, new Rectangle(0, 0, blurW, blurH), cropRect, GraphicsUnit.Pixel);
                }

                // 3. Aplica Box Blur moderado (2 passadas, raio 3) preservando a forma e arte da capa
                ApplyFastBoxBlur(smallBmp, radius: 3, passes: 2);

                // 4. Renderiza em resolução final (960x540) com interpolação bicúbica suave
                var result = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
                using (var gFinal = Graphics.FromImage(result))
                {
                    gFinal.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gFinal.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    gFinal.SmoothingMode = SmoothingMode.HighQuality;

                    gFinal.DrawImage(smallBmp, new Rectangle(0, 0, targetWidth, targetHeight));

                    // 5. Aplica Camada Atmosférica balanceada (~35% a 50% de escurecimento)
                    ApplyVignetteOverlay(gFinal, targetWidth, targetHeight);
                }

                return result;
            }
        }

        private static void ApplyVignetteOverlay(Graphics g, int width, int height)
        {
            // Camada 1: Tint escuro base sutil (33% de escurecimento para harmonizar cores)
            using (var brushBase = new SolidBrush(Color.FromArgb(85, 18, 20, 26)))
            {
                g.FillRectangle(brushBase, 0, 0, width, height);
            }

            // Camada 2: Gradiente Linear Vertical (topo quase livre 6%, base moderada 53% para leitura de cards)
            using (var linBrush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(0, height),
                Color.FromArgb(15, 15, 17, 22),
                Color.FromArgb(135, 12, 14, 18)))
            {
                g.FillRectangle(linBrush, 0, 0, width, height);
            }

            // Camada 3: Vinheta Radial suave nos cantos
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(new Rectangle(0, 0, width, height));
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterPoint = new PointF(width / 2f, height * 0.4f);
                    pgb.CenterColor = Color.FromArgb(0, 0, 0, 0); // Centro transparente
                    pgb.SurroundColors = new[] { Color.FromArgb(100, 10, 12, 16) }; // Bordas suaves
                    g.FillRectangle(pgb, 0, 0, width, height);
                }
            }
        }

        /// <summary>
        /// Aplica Box Blur rápido em memória (com passadas configuráveis).
        /// </summary>
        public static void ApplyFastBoxBlur(Bitmap bmp, int radius, int passes = 3)
        {
            if (radius < 1 || passes < 1) return;

            int w = bmp.Width;
            int h = bmp.Height;

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                int bytes = stride * h;
                byte[] buffer = new byte[bytes];
                byte[] temp = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, bytes);

                // Passadas horizontais e verticais
                for (int pass = 0; pass < passes; pass++)
                {
                    BoxBlurHorizontal(buffer, temp, w, h, stride, radius);
                    BoxBlurVertical(temp, buffer, w, h, stride, radius);
                }

                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, data.Scan0, bytes);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static void BoxBlurHorizontal(byte[] src, byte[] dst, int w, int h, int stride, int r)
        {
            float div = 2 * r + 1;
            for (int y = 0; y < h; y++)
            {
                int rowOffset = y * stride;
                int sumB = 0, sumG = 0, sumR = 0, sumA = 0;

                // Inicializa a janela com o primeiro pixel replicado
                int firstPx = rowOffset;
                sumB = src[firstPx] * (r + 1);
                sumG = src[firstPx + 1] * (r + 1);
                sumR = src[firstPx + 2] * (r + 1);
                sumA = src[firstPx + 3] * (r + 1);

                for (int i = 1; i <= r; i++)
                {
                    int p = rowOffset + Math.Min(i, w - 1) * 4;
                    sumB += src[p];
                    sumG += src[p + 1];
                    sumR += src[p + 2];
                    sumA += src[p + 3];
                }

                for (int x = 0; x < w; x++)
                {
                    int outIdx = rowOffset + x * 4;
                    dst[outIdx] = (byte)(sumB / div);
                    dst[outIdx + 1] = (byte)(sumG / div);
                    dst[outIdx + 2] = (byte)(sumR / div);
                    dst[outIdx + 3] = (byte)(sumA / div);

                    int p1 = rowOffset + Math.Min(x + r + 1, w - 1) * 4;
                    int p2 = rowOffset + Math.Max(x - r, 0) * 4;

                    sumB += src[p1] - src[p2];
                    sumG += src[p1 + 1] - src[p2 + 1];
                    sumR += src[p1 + 2] - src[p2 + 2];
                    sumA += src[p1 + 3] - src[p2 + 3];
                }
            }
        }

        private static void BoxBlurVertical(byte[] src, byte[] dst, int w, int h, int stride, int r)
        {
            float div = 2 * r + 1;
            for (int x = 0; x < w; x++)
            {
                int colOffset = x * 4;
                int sumB = 0, sumG = 0, sumR = 0, sumA = 0;

                sumB = src[colOffset] * (r + 1);
                sumG = src[colOffset + 1] * (r + 1);
                sumR = src[colOffset + 2] * (r + 1);
                sumA = src[colOffset + 3] * (r + 1);

                for (int i = 1; i <= r; i++)
                {
                    int p = Math.Min(i, h - 1) * stride + colOffset;
                    sumB += src[p];
                    sumG += src[p + 1];
                    sumR += src[p + 2];
                    sumA += src[p + 3];
                }

                for (int y = 0; y < h; y++)
                {
                    int outIdx = y * stride + colOffset;
                    dst[outIdx] = (byte)(sumB / div);
                    dst[outIdx + 1] = (byte)(sumG / div);
                    dst[outIdx + 2] = (byte)(sumR / div);
                    dst[outIdx + 3] = (byte)(sumA / div);

                    int p1 = Math.Min(y + r + 1, h - 1) * stride + colOffset;
                    int p2 = Math.Max(y - r, 0) * stride + colOffset;

                    sumB += src[p1] - src[p2];
                    sumG += src[p1 + 1] - src[p2 + 1];
                    sumR += src[p1 + 2] - src[p2 + 2];
                    sumA += src[p1 + 3] - src[p2 + 3];
                }
            }
        }

        private static void SaveAsJpeg(Bitmap bmp, string path, long quality)
        {
            ImageCodecInfo jpegCodec = GetEncoder(ImageFormat.Jpeg);
            if (jpegCodec == null)
            {
                bmp.Save(path, ImageFormat.Jpeg);
                return;
            }

            using (var encoderParams = new EncoderParameters(1))
            using (var qualityParam = new EncoderParameter(Encoder.Quality, quality))
            {
                encoderParams.Param[0] = qualityParam;
                bmp.Save(path, jpegCodec, encoderParams);
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }

        /// <summary>
        /// Varre em segundo plano a pasta de capas e os jogos cadastrados,
        /// convertendo qualquer capa que ainda não possua o respectivo arquivo _hero_blur.jpg.
        /// </summary>
        public Task ScanAndGenerateMissingHeroBackgroundsAsync(string coversDirectory, IEnumerable<GameEntry> games = null)
        {
            return Task.Run(() =>
            {
                try
                {
                    // 1. Processa capas na pasta covers
                    if (!string.IsNullOrEmpty(coversDirectory) && Directory.Exists(coversDirectory))
                    {
                        var files = Directory.GetFiles(coversDirectory);
                        foreach (var file in files)
                        {
                            string ext = Path.GetExtension(file).ToLowerInvariant();
                            if ((ext == ".jpg" || ext == ".jpeg" || ext == ".png") &&
                                !file.EndsWith(HeroBlurSuffix, StringComparison.OrdinalIgnoreCase))
                            {
                                EnsureHeroBackground(file);
                            }
                        }
                    }

                    // 2. Processa capas configuradas nos jogos
                    if (games != null)
                    {
                        foreach (var g in games)
                        {
                            if (!string.IsNullOrWhiteSpace(g.CoverImagePath) && File.Exists(g.CoverImagePath))
                            {
                                EnsureHeroBackground(g.CoverImagePath);
                            }
                        }
                    }
                }
                catch
                {
                    // Falha silenciosa no scanner de background para não travar a aplicação
                }
            });
        }
    }
}
