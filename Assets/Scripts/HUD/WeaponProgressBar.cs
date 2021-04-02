using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponProgressBar : MonoBehaviour {

    public Player player;
    public Slider slider;
    public GameObject sliderContainer;

    void Update() {
        if (player.weaponStatus == WeaponStatus.Reloading) {
            slider.value = 1f - ((player.readyTime - Time.time) / player.weapon[player.wIndexPending].reloadTime);
            sliderContainer.SetActive(true);
        } else if (player.weaponStatus == WeaponStatus.Switching) {
            slider.value = 1f - ((player.readyTime - Time.time) / player.weapon[player.wIndexPending].equipTime);
            sliderContainer.SetActive(true);
        } else {
            sliderContainer.SetActive(false);
        }
    }
}
