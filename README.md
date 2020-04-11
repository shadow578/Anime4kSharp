# Anime4kSharp
Anime4KSharp is a .Net Core library that implements [bloc97's Anime4K](https://github.com/bloc97/Anime4K) Algorithm version [0.9 and 1.0 RC2](https://github.com/bloc97/Anime4K/blob/master/glsl/Anime4K_Adaptive_v0.9.glsl). <br/>
The Algorithm is executed on the CPU, but utilizing all CPU Cores that are available. <br/>
This yields to a conversion time of "only" 4432 ms when upscaling from 1080p to 2160p. This time could possibly reduced with further optimization. </br>
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
<br/>
Since I now have two Versions of the Anime4K algorithm, I also need to pages that explain how it works.<br/>
You can find those pages here:
* [How It Works: Anime4K V0.9](https://github.com/shadow578/Anime4kSharp/blob/master/HOW-IT-WORKS-09.md)
* [How It Works: Anime4K V1.0 RC2](https://github.com/shadow578/Anime4kSharp/blob/master/HOW-IT-WORKS-10RC2.md)

## But Why?
I wrote this port of Anime4K to get a deeper understanding of the Algorithm. <br/>
My personal end goal was to understand Anime4K and the mechanics behind it enough to port it from the reference implementation (which is written in "mpv- syntax" GLSL) to GLSL ES Fragment Shaders. <br/>
You can see the end result in my Video Player App [YAVP](https://github.com/shadow578/YetAnotherVideoPlayer) where I re- implemented Anime4K 0.9 in GLSL ES (running on a mobile GPU that constantly almost dies from stress :P).


## Other Projects That Use Anime4K
* [bloc97/Anime4K](https://github.com/bloc97/Anime4K)
* [yeataro/TD-Anime4K](https://github.com/yeataro/TD-Anime4K)
* [keijiro/UnityAnime4K](https://github.com/keijiro/UnityAnime4K)
* [andraantariksa/Anime4K-rs](https://github.com/andraantariksa/Anime4K-rs)
* [k4yt3x/video2x](https://github.com/k4yt3x/video2x)
* [TianZerL/Anime4KPython](https://github.com/TianZerL/Anime4KPython)
* [TianZerL/Anime4KCPP](https://github.com/TianZerL/Anime4KCPP)
* [TianZerL/Anime4KGo](https://github.com/TianZerL/Anime4KGo)
* [net2cn/Anime4KSharp](https://github.com/net2cn/Anime4KSharp)*

_*This is another Implementation of Anime4K 0.9 in C#, however, net2cn uses C#'s builtin Bitmap class. Still, both mine and net2cn's implementation share many similarities since they are based on the same algorithm._

## Disclaimer
All art assets used are for demonstration and educational purposes. All rights are reserved to their original owners. If you (as a person or a company) own the art and do not wish it to be associated with this project, please contact me (eg by opening a Github issue) and I will gladly take it down.<br/>
Images used for Demonstration are from [Cells At Work](https://myanimelist.net/anime/37141/Hataraku_Saibou_TV).

This repository is created **only** for learning purposes.<br/>
The contents of this repository are provided **"as is"** and are offered without any warranties, but with the hope that they will prove useful to someone. I will **not** be responsible for any damage caused.

#### TL;DR
* The Images used to demonstrate the Algorithm are used for demonstational and educational purposes. If you (as the owner) are not ok with this, please open a Github issue and I will take the Images down.
* You can use the contents of this repository to learn about the Anime4K algorithm. 
* However, if you (somehow) blow your PC up while using this code, you're on your own.
