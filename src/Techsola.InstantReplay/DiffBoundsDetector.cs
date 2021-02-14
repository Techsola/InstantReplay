namespace Techsola.InstantReplay
{
    internal static class DiffBoundsDetector
    {
        // Consider vectorizing if it's worth it, but be careful not to read past the end of the last pixel.

        public static void CropToChanges(Composition first, Composition second, ref UInt16Rectangle boundingRectangle)
        {
            CropTopToChanges(first, second, ref boundingRectangle);
            CropBottomToChanges(first, second, ref boundingRectangle);
            CropLeftToChanges(first, second, ref boundingRectangle);
            CropRightToChanges(first, second, ref boundingRectangle);
        }

        private static void CropTopToChanges(Composition first, Composition second, ref UInt16Rectangle boundingRectangle)
        {
            unsafe
            {
                for (var y = boundingRectangle.Top; y < boundingRectangle.Top + boundingRectangle.Height; y++)
                {
                    var firstPointer = (Color*)first.GetPixelPointer(boundingRectangle.Left, y);
                    var secondPointer = (Color*)second.GetPixelPointer(boundingRectangle.Left, y);

                    for (var x = boundingRectangle.Left; x < boundingRectangle.Left + boundingRectangle.Width; x++)
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
            unsafe
            {
                for (var y = (ushort)(boundingRectangle.Top + boundingRectangle.Height - 1); y > boundingRectangle.Top; y--)
                {
                    var firstPointer = (Color*)first.GetPixelPointer(boundingRectangle.Left, y);
                    var secondPointer = (Color*)second.GetPixelPointer(boundingRectangle.Left, y);

                    for (var x = boundingRectangle.Left; x < boundingRectangle.Left + boundingRectangle.Width; x++)
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
            unsafe
            {
                for (var x = boundingRectangle.Left; x < boundingRectangle.Left + boundingRectangle.Width; x++)
                {
                    for (var y = boundingRectangle.Top; y < boundingRectangle.Top + boundingRectangle.Height; y++)
                    {
                        var firstPointer = (Color*)first.GetPixelPointer(x, y);
                        var secondPointer = (Color*)second.GetPixelPointer(x, y);

                        if (*firstPointer != *secondPointer)
                        {
                            boundingRectangle.Width = (ushort)(boundingRectangle.Width + boundingRectangle.Left - x);
                            boundingRectangle.Left = x;
                            return;
                        }
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
            unsafe
            {
                for (var x = (ushort)(boundingRectangle.Left + boundingRectangle.Width - 1); x > boundingRectangle.Left; x--)
                {
                    for (var y = boundingRectangle.Top; y < boundingRectangle.Top + boundingRectangle.Height; y++)
                    {
                        var firstPointer = (Color*)first.GetPixelPointer(x, y);
                        var secondPointer = (Color*)second.GetPixelPointer(x, y);

                        if (*firstPointer != *secondPointer)
                        {
                            boundingRectangle.Width = (ushort)(x - boundingRectangle.Left + 1);
                            return;
                        }
                    }
                }
            }

            boundingRectangle.Width = 1;
        }
    }
}
