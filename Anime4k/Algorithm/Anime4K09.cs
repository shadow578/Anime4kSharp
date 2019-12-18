﻿using Anime4k.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace Anime4k.Algorithm
{
    /// <summary>
    /// Contains the Anime4K algorithm in Version 0.9
    /// </summary>
    public class Anime4K09 : IAnime4KImplementation
    {
        /// <summary>
        /// The Directory that sub- phase images may be saved to for debugging
        /// </summary>
        public string DebugDirectory { get; set; } = "./debug/";

        /// <summary>
        /// Apply the anime4K algorithm to the image without scaling the image first.
        /// </summary>
        /// <remarks>this function may modify the input image "img"</remarks>
        /// <param name="img">the image to apply the algorithm to</param>
        /// <param name="strengthColor">how strong color push operations should be (scale / 6, range from 0-1)</param>
        /// <param name="strengthGradient">how strong gradient push operations should be (scale / 2, range from 0-1)</param>
        /// <param name="passes">how many times the algorithm should be executed on the image (more = sharper, but slower)</param>
        /// <param name="debugSavePhases">if true, each phase of the pushing algorithm is saved to the specified debug directory path</param>
        /// <returns>the processed image</returns>
        public Image<Rgba32> Push(Image<Rgba32> img, float strengthColor, float strengthGradient, int passes = 2, bool debugSavePhases = false)
        {
            //clamp & calc strenght for algorithm
            strengthColor *= 255f;
            strengthColor = Utility.Clamp(strengthColor, 0, 65535);

            strengthGradient *= 255f;
            strengthGradient = Utility.Clamp(strengthGradient, 0, 65535);

            //create ./debug/ if needed
            if (debugSavePhases && !Directory.Exists(DebugDirectory))
            {
                Directory.CreateDirectory(DebugDirectory);
            }

            //execute laps
            for (int p = 0; p < passes; p++)
            {
                //get luminance into alpha channel
                img = GetLuminance(img);
                if (debugSavePhases) img.Save(Path.Combine(DebugDirectory, $@"{p}-1_get-lum.png"));

                //push color (INCLUDING alpha channel)
                img = PushColor(img, strengthColor);
                if (debugSavePhases) img.Save(Path.Combine(DebugDirectory, $@"{p}-2_push-col.png"));

                //get gradient into alpha channel
                img = GetGradient(img);
                if (debugSavePhases) img.Save(Path.Combine(DebugDirectory, $@"{p}-3_get-grad.png"));

                //push gradient
                img = PushGradient(img, strengthGradient);
                if (debugSavePhases) img.Save(Path.Combine(DebugDirectory, $@"{p}-4_push-grad.png"));
            }

            return img;
        }

        /// <summary>
        /// Compute the Luminance of every Pixel in the image and store it in the image's alpha channel
        /// </summary>
        /// <param name="img">the image to modify, in RGBA32 format</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        Image<Rgba32> GetLuminance(Image<Rgba32> img)
        {
            //process pixels directly
            return img.ChangeEachPixelParallel((x, y, p) =>
            {
                //calc luminance for pixel in range 0 - 255
                float pxLuminance = p.GetLuminance() * 255f;

                //clamp luminance to 0 - 255
                pxLuminance = Utility.Clamp(pxLuminance, 0f, 255f);

                //create new pixel
                return new Rgba32(p.R, p.G, p.B, (byte)Math.Floor(pxLuminance));
            }, true);
        }

        /// <summary>
        /// Push the pixels (including alpha channel) based on the luminance stored in the alpha channel
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <param name="strength">how strong the gradient is pushed (0.0-255.0)</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        Image<Rgba32> PushColor(Image<Rgba32> img, float strength)
        {
            Rgba32 GetLargest(Rgba32 cc, Rgba32 lightest, Rgba32 a, Rgba32 b, Rgba32 c)
            {
                float aR = (cc.R * (255f - strength) + (Utility.Average3(a.R, b.R, c.R) * strength)) / 255f;
                float aG = (cc.G * (255f - strength) + (Utility.Average3(a.G, b.G, c.G) * strength)) / 255f;
                float aB = (cc.B * (255f - strength) + (Utility.Average3(a.B, b.B, c.B) * strength)) / 255f;
                float aA = (cc.A * (255f - strength) + (Utility.Average3(a.A, b.A, c.A) * strength)) / 255f;
                return (aA > lightest.A) ? new Rgba32(aR / 255f, aG / 255f, aB / 255f, aA / 255f) : lightest;
            }

            return img.ChangeEachPixelParallel((x, y, mc) =>
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
                float maxD;
                float minL;
                #endregion

                #region Kernel 0+4
                maxD = Utility.Max3(br.A, bc.A, bl.A);
                minL = Utility.Min3(tl.A, tc.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    lightest = GetLargest(mc, lightest, tl, tc, tr);
                }
                else
                {
                    maxD = Utility.Max3(tl.A, tc.A, tr.A);
                    minL = Utility.Min3(br.A, bc.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        lightest = GetLargest(mc, lightest, br, bc, bl);
                    }
                }
                #endregion

                #region Kernel 1+5
                maxD = Utility.Max3(mc.A, ml.A, bc.A);
                minL = Utility.Min3(mr.A, tc.A, tr.A);

                if (minL > maxD)
                {
                    lightest = GetLargest(mc, lightest, mr, tc, tr);
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, tc.A);
                    minL = Utility.Min3(bl.A, ml.A, bc.A);

                    if (minL > maxD)
                    {
                        lightest = GetLargest(mc, lightest, bl, ml, bc);
                    }
                }
                #endregion

                #region Kernel 2+6
                maxD = Utility.Max3(ml.A, tl.A, bl.A);
                minL = Utility.Min3(mr.A, br.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    lightest = GetLargest(mc, lightest, mr, br, tr);
                }
                else
                {
                    maxD = Utility.Max3(mr.A, br.A, tr.A);
                    minL = Utility.Min3(ml.A, tl.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        lightest = GetLargest(mc, lightest, ml, tl, bl);
                    }
                }
                #endregion

                #region Kernel 3+7
                maxD = Utility.Max3(mc.A, ml.A, tc.A);
                minL = Utility.Min3(mr.A, br.A, bc.A);

                if (minL > maxD)
                {
                    lightest = GetLargest(mc, lightest, mr, br, bc);
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, bc.A);
                    minL = Utility.Min3(tc.A, ml.A, tl.A);

                    if (minL > maxD)
                    {
                        lightest = GetLargest(mc, lightest, tc, ml, tl);
                    }
                }
                #endregion

                //set pixel
                return lightest;
            }, true);
        }

        /// <summary>
        /// Sobel Matrix for sobel operation in GetGradient
        /// </summary>
        static readonly float[,] gradientSobelX = { {-1, 0, 1 },
                                                    {-2, 0, 2 },
                                                    {-1, 0, 1 }};

        /// <summary>
        /// Sobel Matrix for sobel operation in GetGradient
        /// </summary>
        static readonly float[,] gradientSobelY = { {-1, -2, -1 },
                                                    { 0,  0,  0 },
                                                    { 1,  2,  1 }};

        /// <summary>
        /// Compute the Gradient from the alpha channel of the image and store the result in the alpha channel
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        Image<Rgba32> GetGradient(Image<Rgba32> img)
        {
            float SobelAlpha(float[/*3*/,/*3*/] mat, float[/*3*/,/*3*/] col)
            {
                return col[0, 0] * mat[0, 0] + col[0, 1] * mat[0, 1] + col[0, 2] * mat[0, 2]
                     + col[1, 0] * mat[1, 0] + col[1, 1] * mat[1, 1] + col[1, 2] * mat[1, 2]
                     + col[2, 0] * mat[2, 0] + col[2, 1] * mat[2, 1] + col[2, 2] * mat[2, 2];
            }

            //change each pixel
            return img.ChangeEachPixelParallel((x, y, p) =>
            {
                //skip first & last row & collumn
                if (x == 0 || y == 0 || x == img.Width - 1 || y == img.Height - 1)
                {
                    return p;
                }

                //get pixels for sobel calculation
                float[,] sobAlpha = {{img[x-1, y-1].A, img[x, y-1].A, img[x+1, y-1].A },
                                     {img[x-1, y].A,   p.A,           img[x+1, y].A },
                                     {img[x-1, y+1].A, img[x, y+1].A, img[x+1, y+1].A }};

                //do sobel operations on alpha channels
                float dX = SobelAlpha(gradientSobelX, sobAlpha);
                float dY = SobelAlpha(gradientSobelY, sobAlpha);

                //calculate derivata. Dont take square root to save processing time
                double derivataSq = (dX * dX) + (dY * dY);

                //set pixel based on derivata
                if (derivataSq > (255 * 255))
                {
                    return new Rgba32(p.R, p.G, p.B, 0);
                }
                else
                {
                    return new Rgba32(p.R, p.G, p.B, (byte)Math.Floor(255 - Math.Sqrt(derivataSq)));
                }
            }, true);
        }

        /// <summary>
        /// Push the pixels based on the gradient in the alpha channel
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <param name="strength">how strong the gradient is pushed (0.0-255.0)</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        Image<Rgba32> PushGradient(Image<Rgba32> img, float strength)
        {
            Rgba32 GetAverage(Rgba32 cc, Rgba32 a, Rgba32 b, Rgba32 c)
            {
                float aR = (cc.R * (255f - strength) + (Utility.Average3(a.R, b.R, c.R) * strength)) / 255f;
                float aG = (cc.G * (255f - strength) + (Utility.Average3(a.G, b.G, c.G) * strength)) / 255f;
                float aB = (cc.B * (255f - strength) + (Utility.Average3(a.B, b.B, c.B) * strength)) / 255f;
                float aA = (cc.A * (255f - strength) + (Utility.Average3(a.A, b.A, c.A) * strength)) / 255f;
                return new Rgba32(aR / 255f, aG / 255f, aB / 255f, aA / 255f);
            }

            Rgba32 ResetAlpha(Rgba32 c)
            {
                c.A = 255;
                return c;
            }

            return img.ChangeEachPixelParallel((x, y, mc) =>
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

                float maxD;
                float minL;
                #endregion

                #region Kernel 0+4
                maxD = Utility.Max3(br.A, bc.A, bl.A);
                minL = Utility.Min3(tl.A, tc.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    return ResetAlpha(GetAverage(mc, tl, tc, tr));
                }
                else
                {
                    maxD = Utility.Max3(tl.A, tc.A, tr.A);
                    minL = Utility.Min3(br.A, bc.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        return ResetAlpha(GetAverage(mc, br, bc, bl));
                    }
                }
                #endregion

                #region Kernel 1+5
                maxD = Utility.Max3(mc.A, ml.A, bc.A);
                minL = Utility.Min3(mr.A, tc.A, tr.A);

                if (minL > maxD)
                {
                    return ResetAlpha(GetAverage(mc, mr, tc, tr));
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, tc.A);
                    minL = Utility.Min3(bl.A, ml.A, bc.A);

                    if (minL > maxD)
                    {
                        return ResetAlpha(GetAverage(mc, bl, ml, bc));
                    }
                }
                #endregion

                #region Kernel 2+6
                maxD = Utility.Max3(ml.A, tl.A, bl.A);
                minL = Utility.Min3(mr.A, br.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    return ResetAlpha(GetAverage(mc, mr, br, tr));
                }
                else
                {
                    maxD = Utility.Max3(mr.A, br.A, tr.A);
                    minL = Utility.Min3(ml.A, tl.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        return ResetAlpha(GetAverage(mc, ml, tl, bl));
                    }
                }
                #endregion

                #region Kernel 3+7
                maxD = Utility.Max3(mc.A, ml.A, tc.A);
                minL = Utility.Min3(mr.A, br.A, bc.A);

                if (minL > maxD)
                {
                    return ResetAlpha(GetAverage(mc, mr, br, bc));
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, bc.A);
                    minL = Utility.Min3(tc.A, ml.A, tl.A);

                    if (minL > maxD)
                    {
                        return ResetAlpha(GetAverage(mc, tc, ml, tl));
                    }
                }
                #endregion

                return ResetAlpha(mc);
            }, true);
        }
    }
}
