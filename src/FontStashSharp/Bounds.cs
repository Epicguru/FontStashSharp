namespace FontStashSharp
{
	public struct Bounds
	{
		public float X, Y, X2, Y2;

        public override string ToString()
        {
            return $"[x: {X}, y: {Y}, x2: {X2}, y2: {Y2}]";
        }
    }
}
