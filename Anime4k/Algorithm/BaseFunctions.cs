using Anime4k.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Threading.Tasks;

namespace Anime4k.Algorithm
{
    /// <summary>
    /// Contains image functions needed for the Anime4K algorithm
    /// </summary>
    public static class BaseFunctions
    {
        /// <summary>
        /// Compute the Luminance of every Pixel in the image and store it in the image's alpha channel
        /// </summary>
        /// <param name="img">the image to modify, in RGBA32 format</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        public static Image<Rgba32> GetLuminance(this Image<Rgba32> img)
        {
            //process pixels directly
            img.DirectChangeEachPixel((x, y, p) =>
            {
                //calc luminance for pixel
                float pxLuminance = (p.R + p.R + p.G + p.G + p.B + p.B) / 6f;

                //clamp luminance to 0 - 255
                pxLuminance = Utility.Clamp(pxLuminance, 0f, 255f);

                //create new pixel
                return new Rgba32(p.R, p.G, p.B, (byte)Math.Floor(pxLuminance));
            });

            return img;
        }

        /// <summary>
        /// Push the pixels (including alpha channel) based on the luminance stored in the alpha channel
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <param name="strenght">how strong the gradient is pushed (0.0-255.0)</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        public static async Task<Image<Rgba32>> PushColor(this Image<Rgba32> img, float strenght)
        {
            Rgba32 KernelFunc(Rgba32 lightest, Rgba32 mc, params Rgba32[/*6*/] kernel)
            {
                if (kernel.Length != 6) throw new InvalidOperationException("kernel size expected 6!");

                Rgba32 GetLargest(Rgba32 a, Rgba32 b, Rgba32 c)
                {
                    float aR = (mc.R * (255f - strenght) + (Utility.Average3(a.R, b.R, c.R) * strenght)) / 255f;
                    float aG = (mc.G * (255f - strenght) + (Utility.Average3(a.G, b.G, c.G) * strenght)) / 255f;
                    float aB = (mc.B * (255f - strenght) + (Utility.Average3(a.B, b.B, c.B) * strenght)) / 255f;
                    float aA = (mc.A * (255f - strenght) + (Utility.Average3(a.A, b.A, c.A) * strenght)) / 255f;
                    return (aA > lightest.A) ? new Rgba32(aR, aG, aB, aA) : lightest;
                }

                float maxD = Utility.Max3(kernel[0].A, kernel[1].A, kernel[2].A);
                float minL = Utility.Min3(kernel[3].A, kernel[4].A, kernel[5].A);

                if (minL > mc.A && minL > maxD)
                {
                    return GetLargest(kernel[3], kernel[4], kernel[5]);
                }

                maxD = Utility.Max3(kernel[3].A, kernel[4].A, kernel[5].A);
                minL = Utility.Min3(kernel[0].A, kernel[1].A, kernel[2].A);

                if (minL > mc.A && minL > maxD)
                {
                    return GetLargest(kernel[0], kernel[1], kernel[2]);
                }

                return lightest;
            }

            return await img.ChangeEachPixelAsync((x, y, mc) =>
            {
                // Kernel defination:
                // [tl][tc][tr]
                // [ml][mc][mr]
                // [bl][bc][br]

                #region Kernel setup
                //set translation constants
                int xNeg = (x <= 0) ? 0 : -1;
                int xPro = (x >= img.Width - 1) ? 0 : 1;
                int yNeg = (y <= 0) ? 0 : -1;
                int yPro = (y >= img.Height - 1) ? 0 : 1;

                //get pixels:
                //top
                Rgba32 tl = img[x + xNeg, y + yNeg];
                Rgba32 tc = img[x, y + yNeg];
                Rgba32 tr = img[x + xPro, y + yNeg];

                //middle
                Rgba32 ml = img[x + xNeg, y];
                //mc set by pixel function
                Rgba32 mr = img[x + xPro, y];

                //bottom
                Rgba32 bl = img[x + xNeg, y + yPro];
                Rgba32 bc = img[x, y + yPro];
                Rgba32 br = img[x + xPro, y + yPro];

                //default lightest color to current pixel
                Rgba32 lightest = mc;
                #endregion

                //Kernel 0+4
                lightest = KernelFunc(lightest, mc, br, bc, bl, tl, tc, tr);

                //Kernel 1+5
                lightest = KernelFunc(lightest, mc, mc, ml, bc, mr, tc, tr);

                //Kernel 2+6
                lightest = KernelFunc(lightest, mc, ml, tl, bl, mr, br, tr);

                //Kernel 3+7
                lightest = KernelFunc(lightest, mc, mc, ml, tc, mr, br, bc);

                //set pixel
                return lightest;
            });
        }

        /// <summary>
        /// Compute the Gradient from the alpha channel of the image and store the result in the alpha channel
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        public static async Task<Image<Rgba32>> GetGradient(this Image<Rgba32> img)
        {
            float SobelAlpha(float[/*3*/,/*3*/] mat, Rgba32[/*3*/,/*3*/] col)
            {
                return col[0, 0].A * mat[0, 0] + col[0, 1].A * mat[0, 1] + col[0, 2].A * mat[0, 2]
                     + col[1, 0].A * mat[1, 0] + col[1, 1].A * mat[1, 1] + col[1, 2].A * mat[1, 2]
                     + col[2, 0].A * mat[2, 0] + col[2, 1].A * mat[2, 1] + col[2, 2].A * mat[2, 2];
            }

            //init sobel matrixes
            float[,] sobelX = { {-1, 0, 1 },
                                {-2, 0, 2 },
                                {-1, 0, 1 }};

            float[,] sobelY = { {-1, -2, -1 },
                                { 0,  0,  0 },
                                { 1,  2,  1 }};

            //change each pixel
            return await img.ChangeEachPixelAsync((x, y, p) =>
            {
                //skip first & last row & collumn
                if (x == 0 || y == 0 || x == img.Width - 1 || y == img.Height - 1)
                {
                    return p;
                }

                //get pixels for sobel calculation
                Rgba32[,] sobCol = {{img[x-1, y-1], img[x, y-1], img[x+1, y-1] },
                                    {img[x-1, y],   p,           img[x+1, y] },
                                    {img[x-1, y+1], img[x, y+1], img[x+1, y+1] }};

                //do sobel operations on alpha channels
                float dX = SobelAlpha(sobelX, sobCol);
                float dY = SobelAlpha(sobelY, sobCol);

                //calculate derivata
                double derivata = Math.Sqrt((dX * dX) + (dY * dY));

                //set pixel based on derivata
                if (derivata > 255)
                {
                    return new Rgba32(p.R, p.G, p.B, 0);
                }
                else
                {
                    return new Rgba32(p.R, p.G, p.B, (byte)Math.Floor(255 - derivata));
                }
            });
        }

        /// <summary>
        /// Push the pixels based on the gradient in the alpha channel
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <param name="strenght">how strong the gradient is pushed (0.0-255.0)</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        public static async Task<Image<Rgba32>> PushGradient(this Image<Rgba32> img, float strenght)
        {
            Rgba32 KernelFunc(Rgba32 lightest, Rgba32 mc, params Rgba32[/*6*/] kernel)
            {
                if (kernel.Length != 6) throw new InvalidOperationException("kernel size expected 6!");

                Rgba32 GetAverage(Rgba32 a, Rgba32 b, Rgba32 c)
                {
                    float aR = (mc.R * (255f - strenght) + (Utility.Average3(a.R, b.R, c.R) * strenght)) / 255f;
                    float aG = (mc.G * (255f - strenght) + (Utility.Average3(a.G, b.G, c.G) * strenght)) / 255f;
                    float aB = (mc.B * (255f - strenght) + (Utility.Average3(a.B, b.B, c.B) * strenght)) / 255f;
                    float aA = (mc.A * (255f - strenght) + (Utility.Average3(a.A, b.A, c.A) * strenght)) / 255f;
                    return new Rgba32(aR, aG, aB, aA);
                }

                float maxD = Utility.Max3(kernel[0].A, kernel[1].A, kernel[2].A);
                float minL = Utility.Min3(kernel[3].A, kernel[4].A, kernel[5].A);

                if (minL > mc.A && minL > maxD)
                {
                    return GetAverage(kernel[3], kernel[4], kernel[5]);
                }

                maxD = Utility.Max3(kernel[3].A, kernel[4].A, kernel[5].A);
                minL = Utility.Min3(kernel[0].A, kernel[1].A, kernel[2].A);

                if (minL > mc.A && minL > maxD)
                {
                    return GetAverage(kernel[0], kernel[1], kernel[2]);
                }

                return lightest;
            }

            return await img.ChangeEachPixelAsync((x, y, mc) =>
            {
                // Kernel defination:
                // [tl][tc][tr]
                // [ml][mc][mr]
                // [bl][bc][br]

                #region Kernel setup
                //set translation constants
                int xNeg = (x <= 0) ? 0 : -1;
                int xPro = (x >= img.Width - 1) ? 0 : 1;
                int yNeg = (y <= 0) ? 0 : -1;
                int yPro = (y >= img.Height - 1) ? 0 : 1;

                //get pixels:
                //top
                Rgba32 tl = img[x + xNeg, y + yNeg];
                Rgba32 tc = img[x, y + yNeg];
                Rgba32 tr = img[x + xPro, y + yNeg];

                //middle
                Rgba32 ml = img[x + xNeg, y];
                //mc set by pixel function
                Rgba32 mr = img[x + xPro, y];

                //bottom
                Rgba32 bl = img[x + xNeg, y + yPro];
                Rgba32 bc = img[x, y + yPro];
                Rgba32 br = img[x + xPro, y + yPro];

                //default lightest color to current pixel
                Rgba32 lightest = mc;
                #endregion

                //Kernel 0+4
                lightest = KernelFunc(lightest, mc, br, bc, bl, tl, tc, tr);

                //Kernel 1+5
                lightest = KernelFunc(lightest, mc, mc, ml, bc, mr, tc, tr);

                //Kernel 2+6
                lightest = KernelFunc(lightest, mc, ml, tl, bl, mr, br, tr);

                //Kernel 3+7
                lightest = KernelFunc(lightest, mc, mc, ml, tc, mr, br, bc);

                //reset alpha channel since it is no longer needed
                lightest.A = 255;
                return lightest;
            });
        }
    }
}
