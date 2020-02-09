# How Does Anime4K Work (V1.0 RC2)?

As bloc97 described in his [pseudo-preprint](https://github.com/bloc97/Anime4K/blob/master/Preprint.md), the Anime4K algorithm is actually quite simple. <br/>
I, however, will only give a brief overview of the algorithm. If you want to read more, I highly suggest bloc97's preprint.
The Algorithm itself can be summed up in five mayor steps, one of which beign the initial upscaling of the image.

### Step 1: Scaling The Image Up

This Step is all about scaling the image up, like you normally would.<br/>
Let's assume we start with the following image: <br/>
<img src="/ASSETS/step-by-step/10/sbs-0-input-image.png?raw=true" width="300">

We just scale the Image up using a arbitrary Image interpolation algorithm (bloc97 suggests bicubic interpolation, which is also what I went with)  <br/>
This is our Image after scaling it up using bicubic interpolation:<br/>
<img src="/ASSETS/step-by-step/10/sbs-1-scaled-up.png?raw=true" width="300">


### Step 2: Prepare Image Maps

In this Step we create "Maps" from the input Image. These Maps contain information about the Image's Structure and are used in the following steps.<br/>
The Original (GLSL) implementation uses Texture buffers to hold the Data, my implementation just uses a secondary data image instead.<br/>
So, let's start with the first step: <br/>
Calculating the Luminance of the input image into a channel of our data image (I'll call this channel "Luminance" from now on).<br/>
The Luminance is just a grayscale version of the image and looks like this:<br/>
<img src="/ASSETS/step-by-step/10/sbs-2-data-luma.png?raw=true" width="300">

We now use the Luminance we just calculated to create a gradient map of the image. The gradient map is stored into another channel of our data image (I'll call this one "Gradient").<br/>
In the Original GLSL code, this step comes last. However, I figured I could put it earlyer so I can use my data image for temprary values. Also, this step only uses the Luminance anyway...<br/>
Now, let's see what our Gradient map looks like: <br/>
<img src="/ASSETS/step-by-step/10/sbs-3-data-gradient.png?raw=true" width="300">

Cool, so what now? Well, we take our Luminance and run a [Gauss Filter](https://en.wikipedia.org/wiki/Gaussian_filter) over it. 
The result of this filter is then saved into another channel of our data Image. This one I'll call "Gaussed Luminance".<br/>
The Gaussed Luminance looks something like this, tho it's not really interesting since it's just a blurred version of our Luminance: <br/>
<img src="/ASSETS/step-by-step/10/sbs-4-data-luma-gauss.png?raw=true" width="300">

So far, so good. Now, Let's use the Luminance we calculated and our Gaussed Luminance to find where we have lines in our Image.
And let's save that information into another channel of our data image, and call it "Line Map".<br/>
This Line Map just contains, well, the lines in the image. Which we need since we want to enhance them... Quite straight- forward, right?<br/>
Let's just quickly take a look at the Line map: <br/>
<img src="/ASSETS/step-by-step/10/sbs-5-data-line-map.png?raw=true" width="300">

Now, before we get to the spicy stuff, we just quickly have to run another [Gauss Filter](https://en.wikipedia.org/wiki/Gaussian_filter) over the Line Map.<br/>
The result of this step just overrides our previous Line Map. I'll give it a unique name anyway, just for reference. I think "Gaussed Line Map" fits quite well.<br/>
This is how our Gaussed Line Map looks like: <br/>
<img src="/ASSETS/step-by-step/10/sbs-6-data-line-map-gauss.png?raw=true" width="300">


### Step 3: Push Colors Towards The Lines

The next two steps may seem familiar if you already know how Anime4K 0.9 works - that is because they work almost the same.<br/>
In this step all we do is push colors in the image toward the edges. We use the the Luminance and Line Map we calculated previously for this step.<br/>
After doing this, we get a Image that looks nearly the same. But looking at the edges of lines and different colors reveals that they are lighter than in the original (this is especially noticeable at lines) <br/>
<img src="/ASSETS/step-by-step/10/sbs-7-img-thin-lines.png?raw=true" width="300">

The following Image highlights the differences between the upscaled version of the image and the image after the color push step.<br>
As you can see, the changes are mostly around the edges between colors and lines.<br/>
<img src="/ASSETS/step-by-step/10/sbs-7_2-diff-thin-lines.png?raw=true" width="300">


### Step 4: Refine Edges And Lines

In this step, we refine the edges and lines in the Image. To do this, we use the Luminance, Gradient Map and Line Map we calculated previously.<br/>
This also gets rid of some noise that is caused by the interpolation.<br/>
<img src="/ASSETS/step-by-step/10/sbs-8-img-push-lines.png?raw=true" width="300">


### Step 5: Apply FXAA And Finalize

If we were working with Anime4K 0.9, we'd be done now.<br/>
However, Anime4K 1.0+ introduced an additionaly FXAA step to help make the image look smoother (and better).<br/>
Sadly, I couldn't figure out how the FXAA actually works, so I cannot show you a Image of this step.<br/>
If you want to read more about the FXAA step, [here](https://www.geeks3d.com/20110405/fxaa-fast-approximate-anti-aliasing-demo-glsl-opengl-test-radeon-geforce/3/) is an explanation how it works (and how you can implement it).<br/>
<img src="/ASSETS/step-by-step/10/sbs-9-fxaa-until-i-fix-it.jpg?raw=true" width="300">

Hold on - Before we save the Image, we have to dump the alpha channel of the image.<br/>
Otherwise, we'd get a output image that still contains the line map in its alpha channel, resulting in a image with transparent lines (which looks really weird).<br/>
The output of this step is our final output image (assuming we only do one pass):<br/>
<img src="/ASSETS/step-by-step/10/sbs-10-img-reset-alpha.png?raw=true" width="300">


To better show the differences, heres a zoomed- in comparison between bicubic interpolation and the final Anime4K image: <br/>
<img src="/ASSETS/step-by-step/10/sbs-final-compare.png?raw=true" width="1000">


# So What's The Difference Between RC2, RC2_Fast, and RC2_UltraFast

The Difference between the versions is how much they process the Image, resulting in RC2_Fast beign, well, faster than RC2.<br/>
Heres a Table that shows the Key Differences:

|					| RC2			| RC2_Fast		| RC2_UltraFast	|
|-------------------|---------------|---------------|---------------|
| Line Detect		| 7 Px Gaussian	| 5 Px Gaussian	| No Gaussing	|
| Compute Gradient	| Uses Line Map	| Uses Line Map	| No Line Map	|
| Thin Lines		| Yes			| No			| No			|
| FXAA				| Yes			| No			| No			|


### Disclaimer

* This Document is based on Anime4K 1.0 RC2 (FULL). Other (newer) Versions of Anime4K may work somewhat differently
* I wrote this Document almost two months after actively working on the Project. It may not be completely correct.
* I do, by __no__ means claim to be an expert when it comes to Anime4K. (Tho I think this explanation should capture the overall principle quite well)
* Credits for Anime4K go to bloc97