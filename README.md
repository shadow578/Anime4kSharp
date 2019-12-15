# Anime4kSharp
Anime4KSharp is a .Net Core library that implements [bloc97's Anime4K](https://github.com/bloc97/Anime4K) Algorithm version [0.9](https://github.com/bloc97/Anime4K/blob/master/glsl/Anime4K_Adaptive_v0.9.glsl).<br/>
The Algorithm is executed on the CPU, tho utilizing all CPU Cores that are available.

<img src="/ASSETS/image_compare_TOP.png?raw=true" width="1000">

Images are processed in four phases that are executed on a pixel- per- pixel basis. Each phase takes a input image and renders it to a output image. <br/>
This makes it easy to port the algorithm (back) to GLSL fragment shaders.

## Usage (Anime4K CLI)
Call Anime4KCli without arguments to show the help page.

Basic example of upscaling a image 2x:
```cmd
Anime4KCli -input "./test.png" -output "./test-upscaled.png" -scale 2
--OR--
Anime4KCli -i "./test.png" -o "./test-upscaled.png" -s 2
```

## Usage (Anime4K Library)
To use the Library, you have to add [ImageSharp](https://github.com/SixLabors/ImageSharp) to your dependencies.

```csharp
//Load the Imput Image in RGBA32 format
Image<Rgba32> input = Image.Load<Rgba32>("your/input/image.png");

//scale up 2x using anime4k
Image<Rgba32> output = Anime4K09.ScaleAnime4K(input, 2);

//just run Anime4K without upscaling
Image<Rgba32> output = Anime4K09.PushAnime4K(input);

//save finished image
output.Save("your/output/image.png");
```

## How It Works
As bloc97 described in his [pseudo-preprint](https://github.com/bloc97/Anime4K/blob/master/Preprint.md), the Anime4K algorithm is actually  quite simple. <br/>
I, however, will only give a brief overview of the algorithm. If you want to read more, I highly suggest bloc97's preprint.

The Algorithm itself can be summed up in five steps, one of which beign the initial upscaling of the image.

Let's assume we start with the following image: <br/>
<img src="/ASSETS/step-by-step/sbs-0-input-image.png?raw=true" width="300">

We first scale the Image up using a arbitrary Image interpolation algorithm (bloc97 suggests bicubic interpolation, which is also what I went with)  <br/>
<img src="/ASSETS/step-by-step/sbs-1-image-scaled.png?raw=true" width="300">

We then calculate the luminance of every pixel and store it in the otherwise unused alpha channel 
(Image shows B/W representation of alpha channel) <br/>
<img src="/ASSETS/step-by-step/sbs-2-get-luminance.png?raw=true" width="300">

In the third step, the color is pushed based on the luminance information in the alpha channel. <br/>
After doing this, we get a Image that looks nearly the same. But looking at the edges of lines and different colors reveals that they are lighter than in the original (this is especially noticeable at lines) <br/>
<img src="/ASSETS/step-by-step/sbs-3-push-color.png?raw=true" width="300">

The following Image highlights the differences between the upscaled version of the image and the image after the push color step. <br/>
As you can see, the changes are mostly concentrated at edges between colors and lines. <br/>
<img src="/ASSETS/step-by-step/sbs-3_2-push-color-diff.png?raw=true" width="300">

The fourth step now detects the edges of the luminance map using a [Sobel operator](https://en.wikipedia.org/wiki/Sobel_operator). <br/>
The result of the sobel operation is, again, stored in the alpha channel. <br/>
However, the result of the sobel operation is first inverted before storing it. <br/>
(Image shows B/W representation of alpha channel) <br/>
<img src="/ASSETS/step-by-step/sbs-4-get-gradient.png?raw=true" width="300">

The fifth and last step is almost the same as step three, tho instead of getting the lightest color, we use the average color to remove some unwanted noise that is caused by the interpolation. <br/>
Before saving the image, the alpha value of each pixel is reset to completely dump the alpha channel. If this is not done, the output Image will still contain the gradient map. This would result in an image where all edges are transparent.<br/>
The output of this step is our final result (assuming we only do one pass) <br/>
<img src="/ASSETS/step-by-step/sbs-5-push-gradient.png?raw=true" width="300">


To better show the differences, heres a zoomed- in comparison between bicubic interpolation and two passes of Anime4K: <br/>
<img src="/ASSETS/sbs-final-compare.png?raw=true" width="1000">


## But Why?
I wrote this port of Anime4K to get a deeper understanding of the Algorithm. <br/>
My personal end goal was to understand Anime4K and the mechanics behind it enough to port it from the reference implementation (which is written in "mpv- syntax" GLSL) to GLSL ES Fragment Shaders.

## Other Projects That Use Anime4K
* [bloc97/Anime4K](https://github.com/bloc97/Anime4K)
* [yeataro/TD-Anime4K](https://github.com/yeataro/TD-Anime4K)
* [keijiro/UnityAnime4K](https://github.com/keijiro/UnityAnime4K)
* [andraantariksa/Anime4K-rs](https://github.com/andraantariksa/Anime4K-rs)
* [k4yt3x/video2x](https://github.com/k4yt3x/video2x)
* [net2cn/Anime4KSharp](https://github.com/net2cn/Anime4KSharp)*

_*This is another Implementation of Anime4K in C#, however, net2cn uses C#'s builtin Bitmap class. Still, both mine and net2cn's implementation share many similarities since they are based on the same algorithm._

## Disclaimer
All art assets used are for demonstration and educational purposes. All rights are reserved to their original owners. If you (as a person or a company) own the art and do not wish it to be associated with this project, please contact by opening a Github issue and I will gladly take it down.

This repository is created **only** for learning purposes.<br/>
The contents of this repository are provided **"as is"** and are offered without any warranties, but with the hope that they will prove useful to someone. I will **not** be responsible for any damage caused.

#### TL;DR
* The Images used to demonstrate the Algorithm are used for demonstational and educational purposes. If you (as the owner) are not ok with this, please open a Github issue and I will take the Images down.
* You can use the contents of this repository to learn about the Anime4K algorithm. 
* However, if you (somehow) blow your PC up while using this code, you're on your own.
