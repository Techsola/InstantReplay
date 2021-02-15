using System;

namespace Techsola.InstantReplay
{
    internal static class DiffBoundsDetector
    {
        // Consider vectorizing if it's worth it, but be careful not to read past the end of the last pixel.

        /// <summary>
        /// Assumes that <paramref name="first"/> and <paramref name="second"/> have the same number of bytes per pixel
        /// and that their strides are each a multiple of 4 bytes.
        /// </summary>
        public static void CropToChanges(Composition first, Composition second, ref UInt16Rectangle boundingRectangle)
        {
            CropTopToChanges(first, second, ref boundingRectangle);
            CropBottomToChanges(first, second, ref boundingRectangle);
            CropLeftToChanges(first, second, ref boundingRectangle);
            CropRightToChanges(first, second, ref boundingRectangle);
        }

        private static (uint StartIndex, uint Length) Get32BitColumnRange(uint left, uint width, byte bytesPerPixel)
        {
            var startIndex = left * bytesPerPixel / sizeof(uint);
            var endIndex = checked(unchecked((left + width) * bytesPerPixel) - 1) / sizeof(uint);

            return (startIndex, endIndex + 1 - startIndex);
        }

        private static void CropTopToChanges(Composition first, Composition second, ref UInt16Rectangle boundingRectangle)
        {
            if (boundingRectangle.Width == 0 || boundingRectangle.Height == 0) return;

            unsafe
            {
                var uintRange = Get32BitColumnRange(boundingRectangle.Left, boundingRectangle.Width, first.BytesPerPixel);
                var firstUintStride = first.Stride / sizeof(uint);
                var secondUintStride = second.Stride / sizeof(uint);

                for (var y = boundingRectangle.Top; y < boundingRectangle.Top + boundingRectangle.Height; y++)
                {
                    var firstPointer = (uint*)first.PixelDataPointer + (y * firstUintStride) + uintRange.StartIndex;
                    var secondPointer = (uint*)second.PixelDataPointer + (y * secondUintStride) + uintRange.StartIndex;
                    var firstPointerExclusiveEnd = firstPointer + uintRange.Length;

                    while (firstPointer < firstPointerExclusiveEnd)
                    {
                        if (*firstPointer != *secondPointer)
                        {
                            boundingRectangle.Height = (ushort)(boundingRectangle.Height + boundingRectangle.Top - y);
                            boundingRectangle.Top = y;
                            return;
                        }

                        firstPointer++;
                        secondPointer++;
                    }
                }
            }

            boundingRectangle = default;
        }

        /// <summary>
        /// Assumes that the top has already been cropped and therefore the top row contains changes. Also simplifies
        /// the unsigned bounds check.
        /// </summary>
        private static void CropBottomToChanges(Composition first, Composition second, ref UInt16Rectangle boundingRectangle)
        {
            if (boundingRectangle.Width == 0 || boundingRectangle.Height == 0) return;

            unsafe
            {
                var uintRange = Get32BitColumnRange(boundingRectangle.Left, boundingRectangle.Width, first.BytesPerPixel);
                var firstUintStride = first.Stride / sizeof(uint);
                var secondUintStride = second.Stride / sizeof(uint);

                for (var y = (ushort)(boundingRectangle.Top + boundingRectangle.Height - 1); y > boundingRectangle.Top; y--)
                {
                    var firstPointer = (uint*)first.PixelDataPointer + (y * firstUintStride) + uintRange.StartIndex;
                    var secondPointer = (uint*)second.PixelDataPointer + (y * secondUintStride) + uintRange.StartIndex;
                    var firstPointerExclusiveEnd = firstPointer + uintRange.Length;

                    while (firstPointer < firstPointerExclusiveEnd)
                    {
                        if (*firstPointer != *secondPointer)
                        {
                            boundingRectangle.Height = (ushort)(y - boundingRectangle.Top + 1);
                            return;
                        }

                        firstPointer++;
                        secondPointer++;
                    }
                }
            }

            boundingRectangle.Height = 1;
        }

        private static void CropLeftToChanges(Composition first, Composition second, ref UInt16Rectangle boundingRectangle)
        {
            if (boundingRectangle.Width == 0 || boundingRectangle.Height == 0) return;

            unsafe
            {
                var uintRange = Get32BitColumnRange(boundingRectangle.Left, boundingRectangle.Width, first.BytesPerPixel);
                var firstUintStride = first.Stride / sizeof(uint);
                var secondUintStride = second.Stride / sizeof(uint);

                for (var uintIndex = uintRange.StartIndex; uintIndex < uintRange.StartIndex + uintRange.Length; uintIndex++)
                {
                    var firstPointer = (uint*)first.PixelDataPointer + (boundingRectangle.Top * firstUintStride) + uintIndex;
                    var secondPointer = (uint*)second.PixelDataPointer + (boundingRectangle.Top * secondUintStride) + uintIndex;
                    var firstPointerExclusiveEnd = firstPointer + (boundingRectangle.Height * firstUintStride);

                    while (firstPointer < firstPointerExclusiveEnd)
                    {
                        if (*firstPointer != *secondPointer)
                        {
                            // We don't know which changed of the two columns of pixels that this uint column is
                            // overlapping, but it doesn't seem important enough to do a slower scan to find out.
                            // Round to the left to be on the safe side.
                            var x = uintIndex * sizeof(uint) / first.BytesPerPixel;

                            if (boundingRectangle.Left < x)
                            {
                                boundingRectangle.Width = (ushort)(boundingRectangle.Width + boundingRectangle.Left - x);
                                boundingRectangle.Left = (ushort)x;
                            }
                            return;
                        }

                        firstPointer += firstUintStride;
                        secondPointer += secondUintStride;
                    }
                }
            }

            boundingRectangle = default;
        }

        /// <summary>
        /// Assumes that the left has already been cropped and therefore the left row contains changes. Also simplifies
        /// the unsigned bounds check.
        /// </summary>
        private static void CropRightToChanges(Composition first, Composition second, ref UInt16Rectangle boundingRectangle)
        {
            if (boundingRectangle.Width == 0 || boundingRectangle.Height == 0) return;

            unsafe
            {
                var uintRange = Get32BitColumnRange(boundingRectangle.Left, boundingRectangle.Width, first.BytesPerPixel);
                var firstUintStride = first.Stride / sizeof(uint);
                var secondUintStride = second.Stride / sizeof(uint);

                for (var uintIndex = uintRange.StartIndex + uintRange.Length - 1; uintIndex > uintRange.StartIndex; uintIndex--)
                {
                    var firstPointer = (uint*)first.PixelDataPointer + (boundingRectangle.Top * firstUintStride) + uintIndex;
                    var secondPointer = (uint*)second.PixelDataPointer + (boundingRectangle.Top * secondUintStride) + uintIndex;
                    var firstPointerExclusiveEnd = firstPointer + (boundingRectangle.Height * firstUintStride);

                    while (firstPointer < firstPointerExclusiveEnd)
                    {
                        if (*firstPointer != *secondPointer)
                        {
                            // We don't know which changed of the two columns of pixels that this uint column is
                            // overlapping, but it doesn't seem important enough to do a slower scan to find out.
                            // Round to the right to be on the safe side.
                            var x = (((uintIndex + 1) * sizeof(uint)) - 1) / first.BytesPerPixel;

                            boundingRectangle.Width = Math.Min(boundingRectangle.Width, (ushort)(x - boundingRectangle.Left + 1));
                            return;
                        }

                        firstPointer += firstUintStride;
                        secondPointer += secondUintStride;
                    }
                }
            }

            boundingRectangle.Width = 1;
        }
    }
}
