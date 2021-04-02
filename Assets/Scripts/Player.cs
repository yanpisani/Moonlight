using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Assertions;

public struct WeaponStats {
    //public float recoilMaxHeight;
    public float recoilStrengthPerShot;
    public bool fullAuto;
    public float spread;
    //timings
    public float shotDelay; //time added to shotTimer each time we shoot
    public float lastShot; //when the previous shot occured
    public float equipTime; //how long it takes to pull it out
    public float reloadTime; //finish reloading completely
    public float magLoadTime; //ammo has moved from reserve but not quite ready to shoot yet
    //ammo
    public int ammo;
    public int reserve;
    public int maxAmmo;
    public int maxReserve;
    //damage
    public int multiShot;
    public int damageHead;
    public int damageBody;
    public int damageLeg;
}

public enum WeaponStatus {
    Ready,
    Reloading,
    Switching,
    NoAmmo
}

public class Player : MonoBehaviour {
    int health = 9999;

    public float runningSpeed = 4f;
    public float walkingSpeed = 2f;
    float moveSpeed = 4f; //max movement speed
    public float accelSpeed = 20f; //speed gained per second
    public float friction = 0.1f; //0,1 means 10% speed loss per second
    //see https://docs.unity3d.com/ScriptReference/PlayerPrefs.html
    public float mouseSensitivity = 2.5f;
    public float verticalSensMultiplier = 1f;
    public bool lockCursor = true;
    public Transform aimPivot;
    public Camera playerCamera;
    CharacterController cc;
    Vector3 velocity;
    Vector3 tempVector; //avoid new
    RaycastHit hitInfo;

    //weapons
    [HideInInspector]
    public WeaponStats[] weapon = new WeaponStats[2];
    //shooting
    float shotTimer; //time remaining until we are allowed to shoot
    public LineRenderer shotLine;
    public Transform shotLineStartPoint;
    float shotLineTimer;
    float shotLineTimerMax = 0.015f;
    //recoil
    //use Quaternion.RotateTowards to reset recoil towards 0
    Quaternion recoil = Quaternion.identity; //combine the aimPivot with this to get final shot place
    Quaternion recoilTarget = Quaternion.identity; //RotateTowards this when shotTimer > 0
    float recoilSpeedToTarget;
    const float recoilRecoveryRate = 12f;
    float recoilStrength; //how far each shot moves the crosshair, increases per shot
    const float recoilStrengthMax = 0.5f;
    const float recoilStrengthDecayRate = 3f;//15f;
    //Quaternion recoilLerpA = Quaternion.identity; //lerp from A to B in-between shots
    //Quaternion recoilLerpB = Quaternion.identity;
    //Quaternion recoilLerpB; //lerp between identity (A) and this (B)
    //Quaternion recoilTarget; //where (B) will move to using RotateTowards
    //public AnimationCurve[] recoilCurve;
    //float recoilAlpha; //how far we are up recoilCurve //maybe just use slerp?
    //float recoilAlphaVelocity; //how quickly recoilAlpha goes up/down
    //float recoilVelocityDecayRate; //(recoilAlphaVelocityMax / shotDelay) so we can reduce velocity to 0 by the time the next shot happens
    public float cameraFollowsRecoil = 0.5f;
    public bool crosshairFollowsRecoil = true;
    bool triggerWasReleased = true;

    public AudioSource smgSound;
    public AudioSource pistolSound;
    public AudioClip hurtSound;

    //can fire
    public WeaponStatus weaponStatus = WeaponStatus.Ready;
    //[HideInInspector]
    //public float readyStartTime; //when we started reloading or switching, use Time.time
    [HideInInspector]
    public float readyTime; //when the reload or switch will be finished
    float insertMagTime = Mathf.Infinity; //when the ammo moves into the weapon, when Time.time goes above this set it to infinity and change the ammo values

    //crosshair
    RectTransform crosshair;

    void ResetRecoil() {
        recoil = Quaternion.identity;
        recoilTarget = Quaternion.identity;
        recoilStrength = 0f;
    }

    void Awake() {
        cc = GetComponent<CharacterController>();
        QualitySettings.vSyncCount = PlayerPrefs.GetInt("vSync", 0);
        Application.targetFrameRate = PlayerPrefs.GetInt("MaxFPS", 150);
        if (Application.targetFrameRate > 0 && Application.targetFrameRate < 10)
            Application.targetFrameRate = 10;
        //InputSystem.pollingFrequency = 300;

        //weapon shotgun
        weapon[0].shotDelay = 0.2f;
        weapon[0].lastShot = -20f;
        //weapon[0].recoilMaxHeight = -6f;
        weapon[0].recoilStrengthPerShot = 0.0625f;
        weapon[0].fullAuto = false;
        weapon[0].spread = 2f;

        weapon[0].equipTime = 0.7f;
        weapon[0].reloadTime = 2.25f;
        weapon[0].magLoadTime = 1.6f;

        weapon[0].ammo = 2;
        weapon[0].reserve = 9999;
        weapon[0].maxAmmo = 2;
        weapon[0].maxReserve = 36;

        weapon[0].multiShot = 12;
        weapon[0].damageHead = 9;
        weapon[0].damageBody = 3;
        weapon[0].damageLeg = 2;

        //weapon pistol
        weapon[1].shotDelay = 0.2f;
        weapon[1].lastShot = -20f;
        //weapon[1].recoilMaxHeight = -2f;
        weapon[1].recoilStrengthPerShot = 0.25f;
        weapon[1].fullAuto = false;
        weapon[1].spread = 0f;

        weapon[1].equipTime = 0.45f;
        weapon[1].reloadTime = 1.5f;
        weapon[1].magLoadTime = 1f;

        weapon[1].ammo = 7;
        weapon[1].reserve = 21;
        weapon[1].maxAmmo = 7;
        weapon[1].maxReserve = 21;

        weapon[1].multiShot = 1;
        weapon[1].damageHead = 100;
        weapon[1].damageBody = 35;
        weapon[1].damageLeg = 25;
    }

    // Start is called before the first frame update
    void Start() {
        crosshair = GameObject.Find("Crosshair").GetComponent<RectTransform>();
        if (crosshair == null) {
            Debug.LogError("Player could not find the crosshair");
        }
        if (lockCursor) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        Assert.IsFalse(weapon[0].shotDelay == 0f);
        Assert.IsFalse(weapon[1].shotDelay == 0f);
    }

    private void OnApplicationFocus(bool focus) {
        if (focus && lockCursor) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    //input
    Vector2 moveInput;
    public void OnMove(InputAction.CallbackContext context) {
        moveInput = context.ReadValue<Vector2>();
    }
    Vector2 aimInput;
    public void OnAim(InputAction.CallbackContext context) {
        aimInput = context.ReadValue<Vector2>() * 0.022f;
        aimInput.y *= -1;
    }
    bool triggerDown;
    public void OnFire(InputAction.CallbackContext context) {
        triggerDown = context.ReadValueAsButton();
    }
    bool walking;
    public void OnWalk(InputAction.CallbackContext context) {
        walking = context.ReadValueAsButton();
    }
    public void OnSwitchToSmg(InputAction.CallbackContext context) {
        if (!context.performed) return; //only continue if this is a key down, not key up
        SwitchToSmg();
    }
    public void OnSwitchToPistol(InputAction.CallbackContext context) {
        if (!context.performed) return;
        SwitchToPistol();
    }
    public void OnReload(InputAction.CallbackContext context) {
        if (!context.performed) return;
        ReloadWeapon();
    }

    //weapon switching
    public int wIndex = 0;
    public int wIndexPending = 0;
    public void SwitchToSmg() {
        if (wIndexPending != 0) {
            wIndexPending = 0;
            SetSwitchStatus();
        }
    }
    public void SwitchToPistol() {
        if (wIndexPending != 1) {
            wIndexPending = 1;
            SetSwitchStatus();
        }
    }
    public void SwapWeapons(InputAction.CallbackContext context) {
        if (!context.performed) return;
        if (wIndexPending == 0) {
            SwitchToPistol();
        } else {
            SwitchToSmg();
        }
    }

    //status
    void SetSwitchStatus() {
        weaponStatus = WeaponStatus.Switching;
        readyTime = Time.time + weapon[wIndexPending].equipTime;
    }
    void ReloadWeapon() {
        if (weaponStatus == WeaponStatus.Reloading || weaponStatus == WeaponStatus.Switching) return;
        if (weapon[wIndex].reserve <= 0) return;
        if (weapon[wIndex].ammo >= weapon[wIndex].maxAmmo) return;
        weaponStatus = WeaponStatus.Reloading;
        readyTime = Time.time + weapon[wIndex].reloadTime;
        insertMagTime = Time.time + weapon[wIndex].magLoadTime;
    }

    private void Update() {
        //print("hello");
        if (Keyboard.current.zKey.wasPressedThisFrame) {
            tempVector.Set(0.0f, 2.19f, 0.0f);
            transform.position = tempVector;
        }
        if (shotLine.enabled) {
            shotLineTimer -= Time.deltaTime;
            if (shotLineTimer > 0f) {
                shotLine.widthMultiplier = shotLineTimer / shotLineTimerMax;
            } else {
                shotLine.enabled = false;
            }
        }
        if (Keyboard.current.xKey.wasPressedThisFrame/* && pausingAllowed && Time.time > pauseDelay*/) {
            if (lockCursor) {
                UnlockCursor();
            } else {
                LockCursor();
            }
            //menu.SetActive(true);
            //pauseMenu.SetActive(true);
            //pauseDelay = Time.time + 1f;
        }


        //move
        //moveSpeed go towards walking or running speed
        if (!walking) {
            moveSpeed = Mathf.MoveTowards(moveSpeed, runningSpeed, Time.deltaTime * 8f);
        } else {
            moveSpeed = Mathf.MoveTowards(moveSpeed, walkingSpeed, Time.deltaTime * 8f);
        }
        //Vector3 input = Quaternion.Euler(0f, -90f, 0f) * aimPivot.right * Input.GetAxisRaw("Vertical");
        //input += aimPivot.right * Input.GetAxisRaw("Horizontal");
        Vector3 input = Quaternion.Euler(0f, -90f, 0f) * aimPivot.right * moveInput.y;
        input += aimPivot.right * moveInput.x;
        if (input.sqrMagnitude > 1f) input.Normalize();
        velocity = cc.velocity; //gets new speed resulting from hitting walls
        if (velocity != Vector3.zero) {
            //friction
            Vector3 counterVector = velocity * friction;
            if (input == Vector3.zero) {
                //auto counter strafing
                counterVector += velocity.normalized * accelSpeed;
            }
            if (counterVector.sqrMagnitude < 16f) {
                //keep counterVector length above a minimum
                counterVector = counterVector.normalized * 4f;
            }
            counterVector *= Time.deltaTime;
            if (velocity.sqrMagnitude > counterVector.sqrMagnitude) {
                velocity -= counterVector;
            } else {
                velocity = Vector3.zero;
            }
        }
        velocity += input * accelSpeed * Time.deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, moveSpeed);
        cc.SimpleMove(velocity); //simple move does its own deltatime automaticaly
                                 //print(velocity.magnitude);


        if (!lockCursor) return;

        //aim
        bool highClamp = aimPivot.eulerAngles.x > 180f ? true : false;
        //float vertAngle = aimPivot.eulerAngles.x + Input.GetAxisRaw("Mouse Y") * mouseSensitivity * verticalSensMultiplier;
        //float horAngle = aimPivot.eulerAngles.y + Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float vertAngle = aimPivot.eulerAngles.x + aimInput.y * mouseSensitivity * verticalSensMultiplier;
        float horAngle = aimPivot.eulerAngles.y + aimInput.x * mouseSensitivity;
        //clamp x
        if (highClamp)
            vertAngle = Mathf.Clamp(vertAngle, 270f, 450f);
        else
            vertAngle = Mathf.Clamp(vertAngle, -90f, 90f);
        tempVector.Set(vertAngle, horAngle, 0f);
        aimPivot.eulerAngles = tempVector;
        //recoil
        playerCamera.transform.rotation = aimPivot.rotation * Quaternion.Lerp(Quaternion.identity, recoil, cameraFollowsRecoil);
        tempVector.Set(playerCamera.transform.eulerAngles.x, playerCamera.transform.eulerAngles.y, 0f);
        playerCamera.transform.eulerAngles = tempVector;

        //crosshair
        Vector3 dir;
        if (crosshairFollowsRecoil) {
            dir = aimPivot.forward;
            dir = Quaternion.AngleAxis(recoil.eulerAngles.x, aimPivot.right) * dir;
            dir = Quaternion.AngleAxis(recoil.eulerAngles.y, aimPivot.up) * dir;
            dir += aimPivot.position;
        } else {
            dir = playerCamera.transform.position + playerCamera.transform.forward;
        }
        crosshair.position = playerCamera.WorldToScreenPoint(dir);

        //shotTimer
        if (shotTimer > 0f) {
            shotTimer -= Time.deltaTime;
        } else if (shotTimer < 0f) {
            shotTimer = 0f;
        }

        //if (Input.GetButtonUp("Fire1")) {
        if (!triggerDown) {
            triggerWasReleased = true;
        }

        //status
        if (weaponStatus == WeaponStatus.Switching) {
            if (Time.time >= readyTime) {
                wIndex = wIndexPending;
                ResetRecoil();
                weaponStatus = weapon[wIndex].ammo > 0 ? WeaponStatus.Ready : WeaponStatus.NoAmmo;
            }
        } else if (weaponStatus == WeaponStatus.Reloading) {
            if (Time.time >= insertMagTime) {
                weapon[wIndex].reserve -= weapon[wIndex].maxAmmo - weapon[wIndex].ammo;
                weapon[wIndex].ammo = weapon[wIndex].maxAmmo;
                if (weapon[wIndex].reserve < 0) {
                    weapon[wIndex].ammo += weapon[wIndex].reserve;
                    weapon[wIndex].reserve = 0;
                }
            }
            if (Time.time >= readyTime) {
                weaponStatus = WeaponStatus.Ready;
            }
        } else if (weaponStatus == WeaponStatus.NoAmmo) {
            if (Time.time >= readyTime) {
                ReloadWeapon();
            }
        }

        //fire
        //while (Input.GetButton("Fire1") && shotTimer <= 0f && (fullAuto || triggerReleased)) {
        while (weaponStatus == WeaponStatus.Ready && triggerDown && shotTimer <= 0f && (weapon[wIndex].fullAuto || triggerWasReleased)) {
            shotTimer += weapon[wIndex].shotDelay;
            weapon[wIndex].lastShot = Time.time;
            triggerWasReleased = false;
            //set recoil values
            recoilStrength += weapon[wIndex].recoilStrengthPerShot;
            if (recoilStrength > recoilStrengthMax) recoilStrength = recoilStrengthMax;
            recoilTarget = Quaternion.Euler(Random.insideUnitCircle * recoilStrength) * recoil; //new point to move to
            recoilSpeedToTarget = Quaternion.Angle(recoil, recoilTarget) / weapon[wIndex].shotDelay; //how fast we need to move to that new point before next shot arrives
            if (--weapon[wIndex].ammo <= 0) {
                weaponStatus = WeaponStatus.NoAmmo;
                readyTime = Time.time + 0.25f; //adds a small delay so we don't start auto-reloading immediately on the last shot
            }
            Vector3 finalAim = aimPivot.forward;
            finalAim = Quaternion.AngleAxis(recoil.eulerAngles.x, aimPivot.right) * finalAim;
            finalAim = Quaternion.AngleAxis(recoil.eulerAngles.y, aimPivot.up) * finalAim;
            int n = weapon[wIndex].multiShot;
            while (n > 0) {
                --n;
                Vector3 tragectory = finalAim;
                tragectory = Quaternion.AngleAxis(Random.Range(weapon[wIndex].spread*-1, weapon[wIndex].spread), aimPivot.right) * tragectory;
                tragectory = Quaternion.AngleAxis(Random.Range(weapon[wIndex].spread*-1, weapon[wIndex].spread), aimPivot.up) * tragectory;
                bool hit = Physics.Raycast(aimPivot.position, tragectory, out hitInfo);
                Vector3 hitLocation;
                if (hit) { //hit something
                    if (hitInfo.collider.gameObject.layer == 9) { //hit an enemy
                        Enemy e = hitInfo.collider.GetComponentInParent<Enemy>();
                        if (hitInfo.collider.tag == "BodyHitbox") {
                            e.Hit(weapon[wIndex].damageBody);
                        } else if (hitInfo.collider.tag == "HeadHitbox") {
                            e.Hit(weapon[wIndex].damageHead);
                        } else {
                            e.Hit(weapon[wIndex].damageLeg);
                        }
                    }
                    hitLocation = hitInfo.point;
                } else { //missed all
                    hitLocation = aimPivot.position + (tragectory * 50000f);
                }
                SetShotLineHit(hitLocation);
            }
            if (wIndex == 0) {
                smgSound.Play();
            } else {
                pistolSound.Play();
            }
        }
        //recoil
        if (shotTimer > 0f) {
            recoil = Quaternion.RotateTowards(recoil, recoilTarget, recoilSpeedToTarget * Time.deltaTime);
        } else {
            recoil = Quaternion.RotateTowards(recoil, Quaternion.identity, recoilRecoveryRate * Time.deltaTime);
            recoilStrength = Mathf.MoveTowards(recoilStrength, 0f, recoilStrengthDecayRate * /*weapon[wIndex].recoilStrengthPerShot * */ Time.deltaTime);
        }
    }

    public void Hit(int damage) {
        health -= damage;
        //hurtSound.Play();
        AudioSource.PlayClipAtPoint(hurtSound, transform.position);
        if (health <= 0) {
            gameObject.SetActive(false);
        }
    }

    void SetShotLineHit(Vector3 hitLocation) {
        shotLine.SetPosition(0, shotLineStartPoint.position);
        shotLine.SetPosition(1, hitLocation);
        shotLineTimer = shotLineTimerMax;
        shotLine.widthMultiplier = 1f;
        shotLine.enabled = true;
    }

    public Vector3 GetVisiblePosition() {
        return transform.position + Vector3.up * (cc.height/7f);
    }
    public Vector3 GetFootPosition() {
        return transform.position + Vector3.down * (cc.height/2f);
    }

    public void LockCursor() {
        lockCursor = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    public void UnlockCursor() {
        lockCursor = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

}
