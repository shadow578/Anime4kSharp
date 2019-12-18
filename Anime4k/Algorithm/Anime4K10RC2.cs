using Anime4k.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Numerics;

namespace Anime4k.Algorithm
{
    /// <summary>
    /// Contains the Anime4K algorithm in Version 1.0 RC s2
    /// </summary>
    [Obsolete("In Developement, needs more testing!")]
    public class Anime4K010RC2 : IAnime4KImplementation
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
        public Image<Rgba32> Push(Image<Rgba32> img, float strengthColor, float strengthGradient, int passes = 1, bool debugSavePhases = false)
        {
            for (int p = 0; p < passes; p++)
            {
                //execute passes
                #region Prepare DATA image
                //compute luminance into DATA RED Channel
                Image<Rgba32> data = GetLuminance(img);

                //run gauss filter on luma (RED), save to GREEN
                data = ComputeLumaGaussian(data);

                //run line detect in data image, based on luma (RED) and luma gauss (GREEN)
                //save into BLUE
                data = DetectLines(data);

                //RED (luma), GREEN (luma gauss), BLUE (line map)
                if (debugSavePhases) DBSaveImg(data, p, "1_data_line-det-no-gauss");

                //run gauss filter on line map (BLUE), save to BLUE (overwrite)
                data = ComputeLineGaussian(data);

                //create gradient map based on luma (RED), save to ALPHA
                data = ComputeGradient(data);

                //RED (luma), GREEN (luma gauss), BLUE (line map gauss), ALPHA (gradient map)
                if (debugSavePhases) DBSaveImg(data, p, "2_data_line-gauss");
                #endregion

                #region Apply Anime4K to image
                //refine thin lines based on luma (RED) and line map (BLUE),
                //overwrite original image 
                img = PushThinLines(img, data, strengthColor);
                if (debugSavePhases) DBSaveImg(img, p, "3_img_push-thin-lines");

                //refine gradients based on luma (RED), line map (BLUE) and gradient map (ALPHA), 
                //overwrite original image 
                img = PushLines(img, data, strengthGradient);
                if (debugSavePhases) DBSaveImg(img, p, "4_img_push-lines");

                //run 1 iteration of fast fxaa
                //TODO: fix fxaa
                //img = ApplyFxaa(img, data, strengthGradient);
                //if (debugSavePhases) DBSaveImg(img, p, "5_img_fxaa");
                #endregion

                //remove information in alpha channel (only for c#)
                img = ResetAlpha(img);
                if (debugSavePhases) DBSaveImg(img, p, "6_img_reset-alpha");
            }

            return img;
        }

        /// <summary>
        /// Save a sub-stage image to the debug dir
        /// </summary>
        /// <param name="img">the image to save</param>
        /// <param name="pass">which pass this image is from</param>
        /// <param name="name">the name of the image (no .png)</param>
        /// <param name="separateChannels">if true, each channel is saved as separate image</param>
        void DBSaveImg(Image<Rgba32> img, int pass, string name, bool separateChannels = true)
        {
            if (separateChannels)
            {
                string svDir = Path.Combine(DebugDirectory, $"{pass}--{name}");
                if (!Directory.Exists(svDir))
                {
                    Directory.CreateDirectory(svDir);
                }

                //save each channel separately
                Image<Rgba32> chR = img.ChangeEachPixelParallel((x, y, p) => { return new Rgba32(p.R, p.R, p.R, 255); }, false);
                Image<Rgba32> chG = img.ChangeEachPixelParallel((x, y, p) => { return new Rgba32(p.G, p.G, p.G, 255); }, false);
                Image<Rgba32> chB = img.ChangeEachPixelParallel((x, y, p) => { return new Rgba32(p.B, p.B, p.B, 255); }, false);
                Image<Rgba32> chA = img.ChangeEachPixelParallel((x, y, p) => { return new Rgba32(p.A, p.A, p.A, 255); }, false);

                chR.Save(Path.Combine(svDir, "0-RED.png"));
                chG.Save(Path.Combine(svDir, "1-GREEN.png"));
                chB.Save(Path.Combine(svDir, "2-BLUE.png"));
                chA.Save(Path.Combine(svDir, "3-ALPHA.png"));

                //dispose channel images
                chR.Dispose();
                chG.Dispose();
                chB.Dispose();
                chA.Dispose();

                //save original image
                img.Save(Path.Combine(svDir, $"4-ORG.png"));
            }
            else
            {
                img.Save(Path.Combine(DebugDirectory, $"{pass}--{name}.png"));
            }
        }

        /// <summary>
        /// Convert a color value float in range 0f - 255f to a byte of same value
        /// Also apply clamping
        /// </summary>
        /// <param name="c">the color value to unfloat</param>
        /// <returns>the value as byte</returns>
        byte UnFloat(float c)
        {
            //clamp to 0 - 255
            c = Utility.Clamp(c, 0f, 255f);

            //convert to byte
            return (byte)Math.Floor(c);
        }

        /// <summary>
        /// Compute the Luminance of every Pixel in the image and store it in the data image's red channel
        /// This is the "Luma" Stage.
        /// </summary>
        /// <param name="img">the image to modify, in RGBA32 format</param>
        /// <returns>the new luminance map (base for DATA image, with luminance map in RED channel)</returns>
        Image<Rgba32> GetLuminance(Image<Rgba32> img)
        {
            return img.ChangeEachPixelParallel((x, y, p) =>
            {
                //calc luminance for pixel in range 0 - 255
                float pxLuminance = p.GetLuminance() * 255f;

                //clamp luminance to 0 - 255
                pxLuminance = Utility.Clamp(pxLuminance, 0f, 255f);

                //store luminance in data image, red channel (tho this fills ALL channels)
                return new Rgba32(UnFloat(pxLuminance), UnFloat(pxLuminance), UnFloat(pxLuminance), UnFloat(pxLuminance));
            }, false);//dont dispose the original image, we need it later.
        }

        /// <summary>
        /// Apply gaussian blur to the red channel (luminance map) of the data image, store it in the green channel
        /// This is the "ComputeGaussianX" and "ComputeGaussianY" stage.
        /// </summary>
        /// <param name="data">the image that stores additional data</param>
        /// <returns>the modified data image. Now has luminance map in RED and gaussed luminance map in GREEN channel</returns>
        Image<Rgba32> ComputeLumaGaussian(Image<Rgba32> data)
        {
            Rgba32 GetPx(int x, int y)
            {
                x = Utility.Clamp(x, 0, data.Width - 1);
                y = Utility.Clamp(y, 0, data.Height - 1);
                return data[x, y];
            }

            float LumaGauss7(int px, int py, Rgba32 pp, int dx, int dy)
            {
                float gauss = 0f;
                gauss += GetPx(px - (dx * 3), py - (dy * 3)).G * 0.124597f;
                gauss += GetPx(px - (dx * 2), py - (dy * 2)).G * 0.142046f;
                gauss += GetPx(px - (dx * 1), py - (dy * 1)).G * 0.155931f;
                gauss += pp.G * 0.160854f;
                gauss += GetPx(px + (dx * 1), py + (dy * 1)).G * 0.155931f;
                gauss += GetPx(px + (dx * 2), py + (dy * 2)).G * 0.142046f;
                gauss += GetPx(px + (dx * 3), py + (dy * 3)).G * 0.124597f;

                //sanity check clamp
                return Utility.Clamp(gauss, 0f, 255f);
            }

            //compute gaussian X
            data = data.ChangeEachPixelParallel((x, y, p) =>
            {
                //copy luma in R to G so we can reuse the same function.
                //(this is not needed, but makes understanding the function a bit easyer)
                p.G = p.R;

                //apply gaussian blur to luma in green channel on X axis
                p.G = UnFloat(LumaGauss7(x, y, p, 1, 0));
                return p;
            }, true);

            //compute gaussian Y
            return data.ChangeEachPixelParallel((x, y, p) =>
            {
                //apply gaussian blur to luma in green channel on Y axis
                p.G = UnFloat(LumaGauss7(x, y, p, 0, 1));
                return p;
            }, true);
        }

        /// <summary>
        /// Detect Lines in the data image, based on the luminance map(s)
        /// </summary>
        ///  <param name="data">the image that stores additional data</param>
        /// <returns>the modified data image. Now has luminance map in RED, gaussed luminance in GREEN, and line data in BLUE channel</returns>
        Image<Rgba32> DetectLines(Image<Rgba32> data)
        {
            float BlendColorDivide(float bottom, float top)
            {
                if (bottom >= 255f)
                {
                    return bottom;
                }
                else
                {
                    return Math.Min(top / bottom, 255f);
                }
            }

            return data.ChangeEachPixelParallel((x, y, p) =>
            {
                //get luma values clamped between 1 and 254
                float luma = Utility.Clamp(p.R / 255f, 0.001f, 0.999f);
                float lumaGauss = Utility.Clamp(p.G / 255f, 0.001f, 0.999f);

                //calculate line value
                float pseudoLines = BlendColorDivide(luma, lumaGauss);

                //clamp line value
                //original substracts 0.05, 12 should be close enough to equivalent
                pseudoLines = 1f - Utility.Clamp(pseudoLines - 0.05f, 0f, 1f);

                //save line value to BLUE
                p.B = UnFloat(Utility.Clamp(pseudoLines * 255, 0f, 255f));
                return p;
            }, true);
        }

        /// <summary>
        /// Apply gaussian blur to the blue channel (line map) of the data image, store it in the blue channel
        /// This is the "ComputeLineGaussianX" and "ComputeLineGaussianY" stage.
        /// </summary>
        /// <param name="data">the image that stores additional data</param>
        /// <returns>the modified data image. Now has luminance map in RED, gaussed luminance in GREEN, and gaussed line data in BLUE channel</returns>
        Image<Rgba32> ComputeLineGaussian(Image<Rgba32> data)
        {
            Rgba32 GetPx(int x, int y)
            {
                x = Utility.Clamp(x, 0, data.Width - 1);
                y = Utility.Clamp(y, 0, data.Height - 1);
                return data[x, y];
            }

            float LineGauss7(int px, int py, Rgba32 pp, int dx, int dy)
            {
                float gauss = 0f;
                gauss += GetPx(px - (dx * 3), py - (dy * 3)).B * 0.124597f;
                gauss += GetPx(px - (dx * 2), py - (dy * 2)).B * 0.142046f;
                gauss += GetPx(px - (dx * 1), py - (dy * 1)).B * 0.155931f;
                gauss += pp.B * 0.160854f;
                gauss += GetPx(px + (dx * 1), py + (dy * 1)).B * 0.155931f;
                gauss += GetPx(px + (dx * 2), py + (dy * 2)).B * 0.142046f;
                gauss += GetPx(px + (dx * 3), py + (dy * 3)).B * 0.124597f;

                //sanity check clamp
                return Utility.Clamp(gauss, 0f, 255f);
            }

            //compute gaussian X
            data = data.ChangeEachPixelParallel((x, y, p) =>
            {
                //apply gaussian blur to line map in blue channel on X axis
                p.B = UnFloat(LineGauss7(x, y, p, 1, 0));
                return p;
            }, true);


            //compute gaussian Y
            return data.ChangeEachPixelParallel((x, y, p) =>
            {
                //apply gaussian blur to line map in blue channel on Y axis
                p.B = UnFloat(LineGauss7(x, y, p, 0, 1));
                return p;
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
        /// Calculate the gradient map from the luminance (red) in the data image, store it in the alpha channel
        /// This is the "ComputeGradientX" and "ComputeGradientY" stage.
        /// This is (function- wise) the same as "ComputeGradient" in v0.9
        /// </summary>
        /// <param name="data">the image that stores additional data</param>
        /// <returns>the modified data image. Now has luminance map in RED, gaussed luminance in GREEN, gaussed line data in BLUE, and gradient map in ALPHA channel</returns>
        Image<Rgba32> ComputeGradient(Image<Rgba32> data)
        {
            float Sobel(float[/*3*/,/*3*/] mat, float[/*3*/,/*3*/] col)
            {
                return col[0, 0] * mat[0, 0] + col[0, 1] * mat[0, 1] + col[0, 2] * mat[0, 2]
                     + col[1, 0] * mat[1, 0] + col[1, 1] * mat[1, 1] + col[1, 2] * mat[1, 2]
                     + col[2, 0] * mat[2, 0] + col[2, 1] * mat[2, 1] + col[2, 2] * mat[2, 2];
            }

            float GetRed(int x, int y)
            {
                //clamp x and y
                x = Utility.Clamp(x, 0, data.Width - 1);
                y = Utility.Clamp(y, 0, data.Height - 1);

                //return RED channel value at x,y
                return data[x, y].R;
            }

            return data.ChangeEachPixelParallel((x, y, p) =>
            {
                //get red channel values for sobel calculation
                float[,] sobRed = {{GetRed(x-1, y-1), GetRed(x, y-1), GetRed(x+1, y-1)},
                                   {GetRed(x-1, y),   GetRed(x, y),   GetRed(x+1, y) },
                                   {GetRed(x-1, y+1), GetRed(x, y+1), GetRed(x+1, y+1) }};

                //do sobel operations on red channels
                float dX = Sobel(gradientSobelX, sobRed);
                float dY = Sobel(gradientSobelY, sobRed);

                //calculate derivata. Dont take square root to save processing time
                float derivata = (float)Math.Sqrt((dX * dX) + (dY * dY));

                //set pixel alpha to gradient
                p.A = UnFloat(255f - Utility.Clamp(derivata, 0f, 255f));
                return p;
            }, true);
        }


        /// <summary>
        /// Push colors towards edges, refining thin lines and edges
        /// This is the "ThinLines" stage.
        /// This is very similar to PushColor in v0.9, in fact the kernel functions are exactly the same.
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <param name="data">the image that stores additional data. R=Luminance, G=Gaussed Luminance, B=Line map, A=Gradient map</param>
        /// <param name="strength">how strong the gradient is pushed (0.0-255.0) ((scale / 6) * 255)</param>
        /// <returns>the modified image img. data image is NOT modified</returns>
        Image<Rgba32> PushThinLines(Image<Rgba32> img, Image<Rgba32> data, float strength)
        {
            const float LINE_DETECT_TRESHOLD = 15f;//original is 0.06, this should be close enough

            float LineProbe(int x, int y)
            {
                x = Utility.Clamp(x, 0, img.Width - 1);
                y = Utility.Clamp(y, 0, img.Height - 1);

                return data[x, y].B;
            }

            Rgba32 GetPx(int x, int y)
            {
                x = Utility.Clamp(x, 0, img.Width - 1);
                y = Utility.Clamp(y, 0, img.Height - 1);

                Rgba32 px = img[x, y];
                px.A = data[x, y].R;
                return px;
            }

            Rgba32 GetLargest(Rgba32 cc, Rgba32 lightest, Rgba32 a, Rgba32 b, Rgba32 c)
            {
                float aR = (cc.R * (1f - strength) + (Utility.Average3(a.R, b.R, c.R) * strength));
                float aG = (cc.G * (1f - strength) + (Utility.Average3(a.G, b.G, c.G) * strength));
                float aB = (cc.B * (1f - strength) + (Utility.Average3(a.B, b.B, c.B) * strength));
                float aA = (cc.A * (1f - strength) + (Utility.Average3(a.A, b.A, c.A) * strength));

                if (aA > lightest.A)
                {
                    return new Rgba32(UnFloat(aR), UnFloat(aG), UnFloat(aB), UnFloat(aA));
                }
                else
                {
                    return lightest;
                }
            }

            return img.ChangeEachPixelParallel((x, y, p) =>
            {
                //skip if line
                if (LineProbe(x, y) < LINE_DETECT_TRESHOLD)
                {
                    return p;
                }

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

                //get pixels (luminance map is no longer in alpha of img, but in red of data):
                //top
                Rgba32 tl = GetPx(x + xNeg, y + yNeg);
                Rgba32 tc = GetPx(x, y + yNeg);
                Rgba32 tr = GetPx(x + xPro, y + yNeg);

                //middle
                Rgba32 ml = GetPx(x + xNeg, y);
                Rgba32 mc = GetPx(x, y);
                Rgba32 mr = GetPx(x + xPro, y);

                //bottom
                Rgba32 bl = GetPx(x + xNeg, y + yPro);
                Rgba32 bc = GetPx(x, y + yPro);
                Rgba32 br = GetPx(x + xPro, y + yPro);

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
        /// Refines Lines and Edges
        /// This is the "Refine" stage.
        /// This is pretty similar to PushGradient in v0.9, with modification on how strength is calculated for GetAverage and some other differences.
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <param name="data">the image that stores additional data. R=Luminance, G=Gaussed Luminance, B=Line map, A=Gradient map</param>
        /// <param name="strength">how strong the gradient is pushed (0.0-255.0) (scale * 255)</param>
        /// <returns>the modified image img. data image is NOT modified</returns>
        Image<Rgba32> PushLines(Image<Rgba32> img, Image<Rgba32> data, float strength)
        {
            const float LINE_DETECT_MULTI = 6f;
            const float LINE_DETECT_TRESHOLD = 15f;//original is 0.06, this should be close enough

            float LineProbe(int x, int y)
            {
                x = Utility.Clamp(x, 0, img.Width - 1);
                y = Utility.Clamp(y, 0, img.Height - 1);

                return data[x, y].B;
            }

            Rgba32 GetPx(int x, int y)
            {
                x = Utility.Clamp(x, 0, img.Width - 1);
                y = Utility.Clamp(y, 0, img.Height - 1);

                Rgba32 px = img[x, y];
                px.A = data[x, y].A;
                return px;
            }

            Rgba32 GetAverage(Rgba32 cc, Rgba32 a, Rgba32 b, Rgba32 c, float realStrength)
            {
                //calculate average based on strength
                float aR = (cc.R * (1f - realStrength) + (Utility.Average3(a.R, b.R, c.R) * realStrength));
                float aG = (cc.G * (1f - realStrength) + (Utility.Average3(a.G, b.G, c.G) * realStrength));
                float aB = (cc.B * (1f - realStrength) + (Utility.Average3(a.B, b.B, c.B) * realStrength));
                float aA = (cc.A * (1f - realStrength) + (Utility.Average3(a.A, b.A, c.A) * realStrength));

                //create the resulting rgba value
                return new Rgba32(UnFloat(aR), UnFloat(aG), UnFloat(aB), UnFloat(aA));
            }

            return img.ChangeEachPixelParallel((x, y, p) =>
            {
                //skip if line
                if (LineProbe(x, y) < LINE_DETECT_TRESHOLD)
                {
                    return p;
                }

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

                //get pixels (luminance map is no longer in alpha of img, but in red of data):
                //top
                Rgba32 tl = GetPx(x + xNeg, y + yNeg);
                Rgba32 tc = GetPx(x, y + yNeg);
                Rgba32 tr = GetPx(x + xPro, y + yNeg);

                //middle
                Rgba32 ml = GetPx(x + xNeg, y);
                Rgba32 mc = GetPx(x, y);
                Rgba32 mr = GetPx(x + xPro, y);

                //bottom
                Rgba32 bl = GetPx(x + xNeg, y + yPro);
                Rgba32 bc = GetPx(x, y + yPro);
                Rgba32 br = GetPx(x + xPro, y + yPro);

                //default lightest color to current pixel
                float maxD;
                float minL;
                float rs = Utility.Clamp((LineProbe(x, y) / 255f) * strength * LINE_DETECT_MULTI, 0f, 1f);
                #endregion

                #region Kernel 0+4
                maxD = Utility.Max3(br.A, bc.A, bl.A);
                minL = Utility.Min3(tl.A, tc.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    return GetAverage(mc, tl, tc, tr, rs);
                }
                else
                {
                    maxD = Utility.Max3(tl.A, tc.A, tr.A);
                    minL = Utility.Min3(br.A, bc.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        return GetAverage(mc, br, bc, bl, rs);
                    }
                }
                #endregion

                #region Kernel 1+5
                maxD = Utility.Max3(mc.A, ml.A, bc.A);
                minL = Utility.Min3(mr.A, tc.A, tr.A);

                if (minL > maxD)
                {
                    return GetAverage(mc, mr, tc, tr, rs);
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, tc.A);
                    minL = Utility.Min3(bl.A, ml.A, bc.A);

                    if (minL > maxD)
                    {
                        return GetAverage(mc, bl, ml, bc, rs);
                    }
                }
                #endregion

                #region Kernel 2+6
                maxD = Utility.Max3(ml.A, tl.A, bl.A);
                minL = Utility.Min3(mr.A, br.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    return GetAverage(mc, mr, br, tr, rs);
                }
                else
                {
                    maxD = Utility.Max3(mr.A, br.A, tr.A);
                    minL = Utility.Min3(ml.A, tl.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        return GetAverage(mc, ml, tl, bl, rs);
                    }
                }
                #endregion

                #region Kernel 3+7
                maxD = Utility.Max3(mc.A, ml.A, tc.A);
                minL = Utility.Min3(mr.A, br.A, bc.A);

                if (minL > maxD)
                {
                    return GetAverage(mc, mr, br, bc, rs);
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, bc.A);
                    minL = Utility.Min3(tc.A, ml.A, tl.A);

                    if (minL > maxD)
                    {
                        return GetAverage(mc, tc, ml, tl, rs);
                    }
                }
                #endregion

                return mc;
            }, true);
        }

        /// <summary>
        /// Applies 1 iteration of fast FXAA to the image
        /// https://www.geeks3d.com/20110405/fxaa-fast-approximate-anti-aliasing-demo-glsl-opengl-test-radeon-geforce/3/
        /// 
        /// This is the "PostFxaa" stage.
        /// This is new in v1.0 
        /// Honestly I don't really understand most of the fxaa algorithm. I just ported it to c# as best as I could.
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <param name="data">the image that stores additional data. R=Luminance, G=Gaussed Luminance, B=Line map, A=Gradient map</param>
        /// <param name="strength">how strong the gradient is pushed (0.0-255.0) (scale * 255)</param>
        /// <returns>the modified image img. data image is NOT modified</returns>
        Image<Rgba32> ApplyFxaa(Image<Rgba32> img, Image<Rgba32> data, float strength)
        {
            const float FXAA_MIN = 1f / 128f;
            const float FXAA_MULTI = 1f / 8f;
            const float FXAA_SPAN = 8f;

            const float LINE_DETECT_MULTI = 6f;
            const float LINE_DETECT_TRESHOLD = 15f;//original is 0.06, this should be close enough

            float LineProbe(int x, int y)
            {
                x = Utility.Clamp(x, 0, img.Width - 1);
                y = Utility.Clamp(y, 0, img.Height - 1);

                return data[x, y].B;
            }

            float GetLum(Vector4 c)
            {
                return (c.X + c.X + c.Y + c.Y + c.Z + c.Z + c.W + c.W) / 6f;
            }

            float GetPxR(int x, int y)
            {
                x = Utility.Clamp(x, 0, img.Width - 1);
                y = Utility.Clamp(y, 0, img.Height - 1);

                return img[x, y].R;
            }

            Vector4 GetPxVec(Vector2 pos)
            {
                int x = (int)Math.Floor(pos.X);
                int y = (int)Math.Floor(pos.Y);

                x = Utility.Clamp(x, 0, img.Width - 1);
                y = Utility.Clamp(y, 0, img.Height - 1);

                return img[x, y].ToVector4();
            }

            Rgba32 VecToPx(Vector4 vec)
            {
                return new Rgba32(UnFloat(vec.X), UnFloat(vec.Y), UnFloat(vec.Z), UnFloat(vec.W));
            }

            Rgba32 GetAverage(Rgba32 cc, Rgba32 xc, int ccx, int ccy)
            {
                //probe for lines
                float lProb = LineProbe(ccx, ccy);
                if (lProb < LINE_DETECT_TRESHOLD)
                {
                    lProb = 0f;
                }

                //calculate the real strength
                float realStrength = Utility.Clamp((lProb / 255f) * strength * LINE_DETECT_MULTI, 0f, 1f);

                //calculate average based on strength
                float aR = (cc.R * (1f - realStrength) + (xc.R * realStrength));
                float aG = (cc.G * (1f - realStrength) + (xc.G * realStrength));
                float aB = (cc.B * (1f - realStrength) + (xc.B * realStrength));
                float aA = (cc.A * (1f - realStrength) + (xc.A * realStrength));

                //create the resulting rgba value
                return new Rgba32(UnFloat(aR), UnFloat(aG), UnFloat(aB), UnFloat(aA));
            }

            return img.ChangeEachPixelParallel((x, y, p) =>
            {
                //skip if line
                if (LineProbe(x, y) < LINE_DETECT_TRESHOLD)
                {
                    return p;
                }

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

                //get pixels (luminance map is no longer in alpha of img, but in red of data):
                //top
                float tl = GetPxR(x + xNeg, y + yNeg);
                float tc = GetPxR(x, y + yNeg);
                float tr = GetPxR(x + xPro, y + yNeg);

                //middle
                float ml = GetPxR(x + xNeg, y);
                float mc = GetPxR(x, y);
                float mr = GetPxR(x + xPro, y);

                //bottom
                float bl = GetPxR(x + xNeg, y + yPro);
                float bc = GetPxR(x, y + yPro);
                float br = GetPxR(x + xPro, y + yPro);
                #endregion

                //~~ From here on is funky magic stuff I don't yet understand... ~~

                //calculate minl and maxl
                float minL = Math.Min(ml, Math.Min(Math.Min(tl, tr), Math.Min(bl, br)));
                float maxL = Math.Max(ml, Math.Max(Math.Max(tl, tr), Math.Max(bl, br)));

                //calculate dir
                Vector2 dir = new Vector2(-tl - tr + bl + br, tl - tr + bl - br);

                //calc dir reduce
                float dirReduce = Math.Max((tl + tr + bl + br) * (0.25f * FXAA_MULTI), FXAA_MIN);

                //?
                float rcpDirMin = 1f / (Math.Min(Math.Abs(dir.X), Math.Abs(dir.Y)) + dirReduce);

                //¯\_(ツ)_/¯
                dir.X = Math.Min(FXAA_SPAN, Math.Max(-FXAA_SPAN, dir.X * rcpDirMin));//*d (d = size of ONE pixel, in our case 1.0)
                dir.Y = Math.Min(FXAA_SPAN, Math.Max(-FXAA_SPAN, dir.Y * rcpDirMin));//*d ^^

                //OoO
                Vector2 pos = new Vector2(x, y);
                Vector4 rgbA = 0.5f * (GetPxVec(pos + dir * -(1f / 6f))
                                     + GetPxVec(pos + dir * (1f / 6f)));
                Vector4 rgbB = rgbA * 0.5f + 0.25f * (GetPxVec(pos + dir * -0.5f)
                                                    + GetPxVec(pos + dir * 0.5f));

                //¿?
                float lumb = GetLum(rgbB);

                //?
                if (lumb < minL || lumb > maxL)
                {
                    return GetAverage(p, VecToPx(rgbA), x, y);
                }
                else
                {
                    return GetAverage(p, VecToPx(rgbB), x, y);
                }
            }, true);
        }

        /// <summary>
        /// Resets the Alpha channel of every pixel to 255
        /// This does not correspond to any stage in the glsl shaders, since it's not needed for glsl (no alpha blending (enabled) => alpha is ignored when rendering)
        /// This is only needed since we (may) save the image in a format that supports transparency.
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <returns>the modified image</returns>
        Image<Rgba32> ResetAlpha(Image<Rgba32> img)
        {
            return img.ChangeEachPixelParallel((x, y, p) =>
            {
                p.A = 255;
                return p;
            }, true);
        }
    }
}
