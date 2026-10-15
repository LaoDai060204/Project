using UnityEngine;
using System;
namespace PhuocDai.Saga
{
    [Serializable] public sealed class CatalogEntry
    {
        public string id, title, color;
        public int cost, reduction;
        public int[] cores, bp;
        public Sprite art;
        public Definition Rules() { return new Definition { Id=id,Name=title,Color=color,Cost=cost,Reduction=reduction,CoreLevels=cores,BPLevels=bp }; }
    }
    public sealed class SagaCatalog : ScriptableObject { public CatalogEntry[] cards; }
}
