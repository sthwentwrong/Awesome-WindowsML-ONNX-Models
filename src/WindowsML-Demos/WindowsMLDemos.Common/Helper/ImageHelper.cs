﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace WindowsMLDemos.Common.Helper
{
    public class ImageHelper
    {
        public static async Task<SoftwareBitmap> GetImageAsync(IRandomAccessStream stream)
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync();
        }
        /// <summary>
        /// resize video frame with specifical size
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="targetWidth"></param>
        /// <param name="targetHeight"></param>
        /// <returns></returns>
        public async static Task<VideoFrame> ResizeVideoFrameAsync(VideoFrame frame, VideoEncodingProperties encodingProperties, int targetWidth, int targetHeight)
        {
            if (frame != null)
            {
                var destFrame = new VideoFrame(BitmapPixelFormat.Bgra8, targetWidth, targetWidth);

                var sourceWidth = 0u;
                var sourceHeight = 0u;
                if (encodingProperties != null)
                {
                    sourceHeight = encodingProperties.Height;
                    sourceWidth = encodingProperties.Width;
                }
                else
                {
                    if (frame.SoftwareBitmap != null)
                    {
                        sourceHeight = (uint)frame.SoftwareBitmap.PixelHeight;
                        sourceWidth = (uint)frame.SoftwareBitmap.PixelWidth;
                    }
                    else
                    {
                        sourceHeight = (uint)frame.Direct3DSurface.Description.Height;
                        sourceWidth = (uint)frame.Direct3DSurface.Description.Width;
                    }
                }

                var scaleHeigth = targetHeight;
                var scaleWidth = targetWidth;
                var heightOffset = 0;
                var widthOffset = 0;
                if (sourceHeight > sourceWidth)
                {
                    scaleHeigth = (int)sourceWidth * targetHeight / targetWidth;
                    heightOffset = (int)(sourceHeight - scaleHeigth) / 2;
                }
                else
                {
                    scaleWidth = (int)sourceHeight * targetWidth / targetHeight;
                    widthOffset = (int)(sourceWidth - scaleWidth) / 2;
                }

                await frame.CopyToAsync(destFrame, new BitmapBounds
                {
                    X = (uint)widthOffset,
                    Y = (uint)heightOffset,
                    Height = (uint)scaleHeigth,
                    Width = (uint)scaleWidth
                }, null);
                return destFrame;
            }
            return null;
        }
        /// <summary>
        /// pick up a image
        /// </summary>
        /// <returns></returns>
        public static async Task<StorageFile> PickerImageAsync()
        {
            var imagePicker = new FileOpenPicker()
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            imagePicker.ViewMode = PickerViewMode.List;
            imagePicker.FileTypeFilter.Add(".jpg");
            imagePicker.FileTypeFilter.Add(".png");
            imagePicker.FileTypeFilter.Add(".bmp");
            imagePicker.FileTypeFilter.Add(".jpeg");
            var file = await imagePicker.PickSingleFileAsync();
            return file;
        }
        /// <summary>
        /// Resize to square image
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="targetSize"></param>
        /// <param name="format"></param>
        /// <param name="alphaMode"></param>
        /// <returns></returns>
        public static Task<SoftwareBitmap> ResizeImageToSquareAsync(IRandomAccessStream stream,
            int targetSize,
            BitmapPixelFormat format = BitmapPixelFormat.Unknown,
            BitmapAlphaMode alphaMode = BitmapAlphaMode.Ignore)
        {
            return ResizeImageAsync(stream, targetSize, targetSize);
        }
        /// <summary>
        /// Resize image with format
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="targetWidth"></param>
        /// <param name="targetHeight"></param>
        /// <param name="format"></param>
        /// <param name="alphaMode"></param>
        /// <returns></returns>
        public static async Task<SoftwareBitmap> ResizeImageAsync(IRandomAccessStream stream,
            int targetWidth,
            int targetHeight,
            BitmapPixelFormat format = BitmapPixelFormat.Unknown,
            BitmapAlphaMode alphaMode = BitmapAlphaMode.Ignore)
        {

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            var originalPixelWidth = decoder.PixelWidth;
            var originalPixelHeight = decoder.PixelHeight;
            using (var outputStream = new InMemoryRandomAccessStream())
            {
                //create encoder based on decoder of the source file
                var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);
                double widthRatio = (double)targetWidth / originalPixelWidth;
                double heightRatio = (double)targetHeight / originalPixelHeight;
                uint aspectHeight = (uint)targetHeight;
                uint aspectWidth = (uint)targetWidth;
                uint cropX = 0, cropY = 0;
                var scaledWith = (uint)targetWidth;
                var scaledHeight = (uint)targetHeight;
                if (originalPixelWidth > originalPixelHeight)
                {
                    aspectWidth = (uint)(heightRatio * originalPixelWidth);
                    cropX = (aspectWidth - aspectHeight) / 2;
                }
                else
                {
                    aspectHeight = (uint)(widthRatio * originalPixelHeight);
                    cropY = (aspectHeight - aspectWidth) / 2;
                }
                //you can adjust interpolation and other options here, so far linear is fine for thumbnails
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;
                encoder.BitmapTransform.ScaledHeight = aspectHeight;
                encoder.BitmapTransform.ScaledWidth = aspectWidth;
                encoder.BitmapTransform.Bounds = new BitmapBounds()
                {
                    Width = scaledWith,
                    Height = scaledHeight,
                    X = cropX,
                    Y = cropY,
                };
                await encoder.FlushAsync();

                //get reszied image
                var outputDecoder = await BitmapDecoder.CreateAsync(outputStream);
                var outputImg = await outputDecoder.GetSoftwareBitmapAsync();
                // apply alpha mode
                outputImg = SoftwareBitmap.Convert(outputImg, outputImg.BitmapPixelFormat, alphaMode);
                //apply piexl format
                if (format != BitmapPixelFormat.Unknown && format != decoder.BitmapPixelFormat)
                {
                    return SoftwareBitmap.Convert(outputImg, format);
                }
                return outputImg;
            }
        }

        public static async Task<SoftwareBitmap> GetImageFromTensorFloatDataAsync(TensorFloat16Bit outputImage, uint imageWidth, uint imageHeigth, double DpiX, double DpiY)
        {
            var outData = outputImage.GetAsVectorView().ToArray();
            var lineLength = outData.Length / 3;
            var newImageData = new byte[4 * lineLength];

            if (outData.Length > 0)
            {
                var bData = outData.Take(lineLength).ToArray();
                var rData = outData.Skip(lineLength * 2).Take(lineLength).ToArray();
                var gData = outData.Skip(lineLength).Take(lineLength).ToArray();


                for (var i = 0; i < lineLength; i++)
                {
                    var b = (bData[i]);//* 255);
                    if (b < 0)
                    {
                        b = 0;
                    }
                    else if (b > 255)
                    {
                        b = 255;
                    }
                    newImageData[i * 4 + 0] = (byte)b;
                    var g = (gData[i]);// * 255);
                    if (g < 0)
                    {
                        g = 0;
                    }
                    else if (g > 255)
                    {
                        g = 255;
                    }
                    newImageData[i * 4 + 1] = (byte)g;
                    var r = (rData[i]);// * 255);
                    if (r < 0)
                    {
                        r = 0;
                    }
                    else if (r > 255)
                    {
                        r = 255;
                    }
                    newImageData[i * 4 + 2] = (byte)r;
                    newImageData[i * 4 + 3] = 255;
                }

            }

            using (var ms = new InMemoryRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, ms);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, imageWidth, imageHeigth, DpiX, DpiY, newImageData);
                await encoder.FlushAsync();

                var decoder = await BitmapDecoder.CreateAsync(ms);
                var sbmp = await decoder.GetSoftwareBitmapAsync();
                //sbmp = SoftwareBitmap.Convert(sbmp, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
                return sbmp;
            }
        }
    }
}
