using System.Collections.Generic;

namespace Techsola.InstantReplay
{
    partial class GifWriter
    {
        private sealed class GraphNode
        {
            private Dictionary<byte, GraphNode>? children;

            public GraphNode(ushort code)
            {
                Code = code;
            }

            public ushort Code { get; }

            public GraphNode GetOrAddChildNode(byte childKey, ushort nextCode, out bool didAdd)
            {
                children ??= new();

                if (!children.TryGetValue(childKey, out var childNode))
                {
                    childNode = new(nextCode);
                    children.Add(childKey, childNode);
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
