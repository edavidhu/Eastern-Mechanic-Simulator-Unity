using UnityEngine;

// Ez a sor teszi lehetővé, hogy a Unity jobb-klikk menüjéből létrehozzunk ilyet!
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;       // A tárgy neve (pl. "Lada Motor")
    public Sprite icon;           // A 2D piktogram, ami megjelenik a Hotbaron
    public GameObject dropPrefab; // A 3D-s modell, amit ledobsz a földre, ha kidobod a zsebedből
    public int maxStack = 64;     // <-- EZ AZ ÚJ SOR! Hány darab fér el egymáson?
    
    // Ide később jöhet még bármi: súly, ár, stb. De kezdésnek ez a 3 kell!
}