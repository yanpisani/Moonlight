using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponStatusHUD : MonoBehaviour {
    public Player player;

    int slot0Ammo;
    public Text slot0AmmoText;
    int slot0Reserve;
    public Text slot0ReserveText;
    int slot1Ammo;
    public Text slot1AmmoText;
    int slot1Reserve;
    public Text slot1ReserveText;

    int wIndexPending;
    public Image slot0Background;
    public Image slot1Background;
    public Color slotColor;

    void FixedUpdate() {
        Check(ref slot0Ammo, player.weapon[0].ammo, ref slot0AmmoText);
        Check(ref slot0Reserve, player.weapon[0].reserve, ref slot0ReserveText);
        Check(ref slot1Ammo, player.weapon[1].ammo, ref slot1AmmoText);
        Check(ref slot1Reserve, player.weapon[1].reserve, ref slot1ReserveText);
        if (wIndexPending != player.wIndexPending) {
            wIndexPending = player.wIndexPending;
            if (wIndexPending == 0) {
                slot0Background.color = slotColor;
                slot1Background.color = Color.clear;
            } else {
                slot0Background.color = Color.clear;
                slot1Background.color = slotColor;
            }
        }
    }
    void Check(ref int n, int p, ref Text text) {
        if (n != p) {
            n = p;
            text.text = p.ToString();
        }
    }
}
