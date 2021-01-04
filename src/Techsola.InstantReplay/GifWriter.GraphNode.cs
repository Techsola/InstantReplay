namespace Techsola.InstantReplay
{
    partial class GifWriter
    {
        private sealed class GraphNode
        {
            // Inlined fields for sequential scan
            private GraphNode? childNode1, childNode2, childNode3, childNode4;
            private byte childKey1, childKey2, childKey3, childKey4;

            private GraphNode?[]? randomAccess;

            public GraphNode(ushort code)
            {
                Code = code;
            }

            public ushort Code { get; }

            public GraphNode GetOrAddChildNode(byte childKey, ushort nextCode, out bool didAdd)
            {
                GraphNode? childNode;

                if (randomAccess is not null)
                {
                    childNode = randomAccess[childKey];
                    if (childNode is null)
                    {
                        childNode = new(nextCode);
                        randomAccess[childKey] = childNode;
                        didAdd = true;
                    }
                    else
                    {
                        didAdd = false;
                    }

                    return childNode;
                }

                childNode =
                    childKey == childKey1 ? childNode1 :
                    childKey == childKey2 ? childNode2 :
                    childKey == childKey3 ? childNode3 :
                    childKey == childKey4 ? childNode4 :
                    null;

                if (childNode is not null)
                {
                    didAdd = false;
                    return childNode;
                }

                childNode = new(nextCode);

                if (childNode1 is null) { childNode1 = childNode; childKey1 = childKey; }
                else if (childNode2 is null) { childNode2 = childNode; childKey2 = childKey; }
                else if (childNode3 is null) { childNode3 = childNode; childKey3 = childKey; }
                else if (childNode4 is null) { childNode4 = childNode; childKey4 = childKey; }
                else
                {
                    randomAccess = new GraphNode[256];
                    randomAccess[childKey] = childNode;
                }

                didAdd = true;
                return childNode;
            }
        }
    }
}
