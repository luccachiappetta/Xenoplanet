using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class PlayerUpdatedBow : MonoBehaviour
{
    private PlayerUpdatedController _playerUpdatedController; // TODO: HMMMMMMM?
    // private CinemachineFreeLook.Orbit[] _startOrbits;
    
    [Header("Cameras")]
    [SerializeField] private GameObject moveCamera;
    [SerializeField] private GameObject aimCamera;
    [SerializeField] private Transform mainCamera;
    
    [Header("References")]
    [SerializeField] private CrosshaireController crosshairController;
    [SerializeField] private GameObject[] arrows;
    [SerializeField] private CinemachineImpulseSource impulseSource;
    
    [Header("References")]
    [SerializeField] private AudioSource bowDrawAudio;
    [SerializeField] private AudioSource bowFireAudio;
    [SerializeField] private Animator _animator;
    [SerializeField] private GameObject bow;
    [SerializeField] private GameObject backBow;

    [Header("Values")]
    [SerializeField] private float chargeTimeCoefficient;
    [SerializeField] private float chargeTimeExponent;
    [SerializeField] private float maxImpulseForce;
    [SerializeField] private float sphereCastRadius;
    [SerializeField] private float maxAimDistance;
    
    [SerializeField] private LayerMask enemiesLayerMask;
    
    /*[SerializeField] private float fovReduction;
    [SerializeField] private float fov;*/
    public UnityEvent onFire;
    
    private float _aimProgress;
    private float _chargeTime = 0f;
    private float _strength = 0f;
    
    private int _selectedArrowIndex = 0;
    private int _numArrows;

    private void Start()
    {
        _playerUpdatedController = GetComponent<PlayerUpdatedController>();
        Inventory.OnUpdateCount.AddListener(UpdateNumArrows);
        // _startOrbits = _playerUpdatedController.thirdPersonCamera.m_Orbits;
    }
    
    private void UpdateNumArrows()
    {
        string arrowName = arrows[_selectedArrowIndex].name + 's';
        _numArrows = Inventory.Instance.GetItemCount(arrowName);
        HUDController.Instance.SetNumArrows(_numArrows);
    }
    
    public void CycleArrow()
    {
        // Start by cycling arrow index
        _selectedArrowIndex++;
        if (_selectedArrowIndex >= arrows.Length)
            _selectedArrowIndex = 0;
        
        UpdateNumArrows();
        HUDController.Instance.SetArrow(arrows[_selectedArrowIndex].name);
    }

    public void Aim(bool input)
    {
        if (input)
        {
            if (!aimCamera.activeInHierarchy)
            {
                aimCamera.SetActive(true);
                moveCamera.SetActive(false);
                
                if (_numArrows > 0)
                    bowDrawAudio.Play();
            }
            ChargeArrow();
            bow.SetActive(true);
            backBow.SetActive(false);
            
        }
        else if(!input && !moveCamera.activeInHierarchy)
        {
            moveCamera.SetActive(true);
            aimCamera.SetActive(false);
            bowDrawAudio.Stop();
            
            _chargeTime = 0;
            bow.SetActive(false);
            backBow.SetActive(true);
        }        
    }

    public void Fire()
    {
        // Debug.Log(aimCamera.activeInHierarchy || _numArrows <= 0);
        if (!aimCamera.activeInHierarchy || _numArrows <= 0)
            return;
        
        Vector3 spawnPosition = _playerUpdatedController.mainCamera.transform.position; 
        Quaternion arrowDirection = Quaternion.LookRotation(_playerUpdatedController.mainCamera.transform.forward);

        var arrowInstance = Instantiate(arrows[_selectedArrowIndex], spawnPosition, arrowDirection);
        arrowInstance.GetComponent<Arrow>().Fire(_strength); // Add force to the arrow equal to strength using arrow API
        _chargeTime = 0;

        bowFireAudio.Play();
        bowDrawAudio.Play();
        Inventory.Instance.UpdateItemCount(arrows[_selectedArrowIndex].name + 's', -1);  // Remove arrow from inventory
        impulseSource.GenerateImpulse(_strength * maxImpulseForce); // Generate an impulse when arrows are fired
        onFire.Invoke();
        
        _animator.SetTrigger("Fire");
    }
    
    private void ChargeArrow()
    {
        if (_numArrows <= 0)
            return;
        
        _chargeTime += Time.fixedDeltaTime; 
        _strength = 1 - 1 / (Mathf.Pow((_chargeTime * chargeTimeCoefficient), chargeTimeExponent) + 1);

        // _playerUpdatedController.aimCamera.m_Lens.FieldOfView = fov - _strength * fovReduction;
        crosshairController.SetCrossHairWidth(_strength);
    }
    
    
    private Quaternion CalculateAimDirection(Vector3 spawnPosition)
    {
        Transform mainCameraTransform = _playerUpdatedController.mainCamera.transform;
        
        Vector3 origin = mainCameraTransform.position;
        Vector3 direction = mainCameraTransform.forward;
        Ray ray = new Ray(origin, direction);

        Vector3 aimDirection;
        if (Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxAimDistance, enemiesLayerMask))
            aimDirection = hit.point - spawnPosition;
        else
            aimDirection = mainCameraTransform.forward;
        
        return Quaternion.LookRotation(aimDirection);
    }
    
    private CinemachineFreeLook.Orbit LerpOrbit(CinemachineFreeLook.Orbit a,CinemachineFreeLook.Orbit b, float t)
    {
        // Debug.Log("lerp");

        CinemachineFreeLook.Orbit result = new CinemachineFreeLook.Orbit();
        result.m_Height = Mathf.Lerp(a.m_Height, b.m_Height, t);
        result.m_Radius = Mathf.Lerp(a.m_Radius, b.m_Radius, t);

        return result;
    }
    
    
}