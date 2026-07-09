using UnityEngine;

public class ItemObject : MonoBehaviour
{
    public ItemData itemData;

    [Header("Kézben tartás finomhangolása")]
    public Vector3 holdPositionOffset = Vector3.zero;
    public Vector3 holdRotationOffset = Vector3.zero;

    // ÚJ: Megjegyzi, hogy épp melyik mágnesen csücsül a kocsin!
    [HideInInspector] public SnapPoint currentSnapPoint; 
}