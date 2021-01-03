namespace Techsola.InstantReplay
{
    partial class GifWriter
    {
        private sealed class GraphNode
        {
            private GraphNode?[]? children;

            public GraphNode(ushort code)
            {
                Code = code;
            }

            public ushort Code { get; }

            public GraphNode GetOrAddChildNode(byte childKey, ushort nextCode, out bool didAdd)
            {
                children ??= new GraphNode[256];

                var childNode = children[childKey];
                if (childNode is null)
                {
                    childNode = new(nextCode);
                    children[childKey] = childNode;
                    didAdd = true;
                }
                else
                {
                    didAdd = false;
                }

                return childNode;
            }
        }
    }
}
