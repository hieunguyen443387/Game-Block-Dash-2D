using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IceBlockConfig", menuName = "Game/Ice Block Config")]
public class IceBlockConfig : ScriptableObject
{
    [System.Serializable]
    public struct IceSizeData
    {
        public string sizeName;      // Tên hiển thị (Ví dụ: Block 1x1, Block 1x2, Block 2x2...)
        public Vector2 boxSize;      // Kích thước Scale (Ví dụ: (1,1), (1,2)...)
        public Sprite iceSprite;     // Sprite tương ứng với size đó
    }

    public List<IceSizeData> iceSizes = new List<IceSizeData>();
}