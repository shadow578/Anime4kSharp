# How Does Anime4K Work (V0.9)?

As bloc97 described in his [pseudo-preprint](https://github.com/bloc97/Anime4K/blob/master/Preprint.md), the Anime4K algorithm is actually quite simple. <br/>
I, however, will only give a brief overview of the algorithm. If you want to read more, I highly suggest bloc97's preprint.
The Algorithm itself can be summed up in five steps, one of which beign the initial upscaling of the image.

Let's assume we start with the following image: <br/>
<img src="/ASSETS/step-by-step/09/sbs-0-input-image.png?raw=true" width="300">

We first scale the Image up using a arbitrary Image interpolation algorithm (bloc97 suggests bicubic interpolation, which is also what I went with)  <br/>
<img src="/ASSETS/step-by-step/09/sbs-1-image-scaled.png?raw=true" width="300">

We then calculate the luminance of every pixel and store it in the otherwise unused alpha channel 
(Image shows B/W representation of alpha channel) <br/>
<img src="/ASSETS/step-by-step/09/sbs-2-get-luminance.png?raw=true" width="300">

In the third step, the color is pushed based on the luminance information in the alpha channel. <br/>
After doing this, we get a Image that looks nearly the same. But looking at the edges of lines and different colors reveals that they are lighter than in the original (this is especially noticeable at lines) <br/>
<img src="/ASSETS/step-by-step/09/sbs-3-push-color.png?raw=true" width="300">

The following Image highlights the differences between the upscaled version of the image and the image after the push color step. <br/>
As you can see, the changes are mostly concentrated at edges between colors and lines. <br/>
<img src="/ASSETS/step-by-step/09/sbs-3_2-push-color-diff.png?raw=true" width="300">

The fourth step now detects the edges of the luminance map using a [Sobel operator](https://en.wikipedia.org/wiki/Sobel_operator). <br/>
The result of the sobel operation is, again, stored in the alpha channel. <br/>
However, the result of the sobel operation is first inverted before storing it. <br/>
(Image shows B/W representation of alpha channel) <br/>
<img src="/ASSETS/step-by-step/09/sbs-4-get-gradient.png?raw=true" width="300">

The fifth and last step is almost the same as step three, tho instead of getting the lightest color, we use the average color to remove some unwanted noise that is caused by the interpolation. <br/>
Before saving the image, the alpha value of each pixel is reset to completely dump the alpha channel. If this is not done, the output Image will still contain the gradient map. This would result in an image where all edges are transparent.<br/>
The output of this step is our final result (assuming we only do one pass) <br/>
<img src="/ASSETS/step-by-step/09/sbs-5-push-gradient.png?raw=true" width="300">


To better show the differences, heres a zoomed- in comparison between bicubic interpolation and two passes of Anime4K: <br/>
<img src="/ASSETS/step-by-step/09/sbs-final-compare.png?raw=true" width="1000">

### Disclaimer
* This Document is based on Anime4K 0.9. Other (newer) Versions of Anime4K do work somewhat differently
* I do, by __no__ means claim to be an expert when it comes to Anime4K. (Tho I think this explanation should capture the overall principle quite well)
* Credits for Anime4K go to bloc97