﻿using Leap.Unity;
using System.Collections.Generic;
using UnityEngine;

public class PinchStrokeProcessor : MonoBehaviour {

  private const float MIN_THICKNESS_MIN_SEGMENT_LENGTH = 0.001F;
  private const float MAX_THICKNESS_MIN_SEGMENT_LENGTH = 0.007F;
  private const float MAX_SEGMENT_LENGTH = 0.03F;

  public PinchDetector _pinchDetector;
  [Tooltip("Used to stop drawing if the pinch detector is grabbing a UI element.")]
  public WearableManager _wearableManager;
  public HistoryManager _historyManager;
  public GameObject _ribbonParentObject;
  public RibbonIO _ribbonIO;
  public FilterIndexTipColor _colorFilter;
  public FilterApplyThickness _thicknessFilter;

  private bool _paintingStroke = false;
  private StrokeProcessor _strokeProcessor;
  private bool _firstStrokePointAdded = false;
  private Vector3 _lastStrokePointAdded = Vector3.zero;
  private float _timeSinceLastAddition = 0F;

  private Vector3 leftHandEulerRotation = new Vector3(0F, 180F, 0F);
  private Vector3 rightHandEulerRotation = new Vector3(0F, 180F, 0F);

  private StrokeRibbonRenderer _ribbonRenderer;

  void Start() {
    _strokeProcessor = new StrokeProcessor();

    // Set up and register filters.
    FilterPositionMovingAverage movingAvgFilter = new FilterPositionMovingAverage(6);
    _strokeProcessor.RegisterStrokeFilter(movingAvgFilter);
    FilterPitchYawRoll pitchYawRollFilter = new FilterPitchYawRoll();
    _strokeProcessor.RegisterStrokeFilter(pitchYawRollFilter);

    _strokeProcessor.RegisterStrokeFilter(_colorFilter);
    _strokeProcessor.RegisterStrokeFilter(_thicknessFilter);

    // Set up and register renderers.
    GameObject rendererObj = new GameObject();
    _ribbonRenderer = rendererObj.AddComponent<StrokeRibbonRenderer>();
    _ribbonRenderer.OnMeshStrokeFinalized += DoOnMeshStrokeFinalized;
    _strokeProcessor.RegisterStrokeRenderer(_ribbonRenderer);
  }

  void Update() {
    if (_pinchDetector.IsActive && !_paintingStroke) {
      if (!_wearableManager.IsPinchDetectorGrabbing(_pinchDetector)) {
        // TODO HACK FIXME
        Color color = new Color(0F, 0F, 0F, 0F);
        try {
          color = _pinchDetector.GetComponentInParent<IHandModel>().GetComponentInChildren<IndexTipColor>().GetColor();
        }
        catch (System.NullReferenceException) { }
        if (color.a > 0.99F) {
          BeginStroke();
          _paintingStroke = true;
        }
      }
    }
    else if (_pinchDetector.IsActive && _paintingStroke) {
      UpdateStroke();
    }
    else if (!_pinchDetector.IsActive && _paintingStroke) {
      EndStroke();
      _paintingStroke = false;
    }
  }

  private void BeginStroke() {
    _strokeProcessor.BeginStroke();
  }

  private List<Vector3> _cachedPositions = new List<Vector3>();
  private List<float> _cachedDeltaTimes = new List<float>();
  private void UpdateStroke() {
    Vector3 strokePosition = _pinchDetector.Position;

    if (_firstStrokePointAdded) {
      float posDelta = Vector3.Distance(_lastStrokePointAdded, strokePosition);
      if (posDelta > MAX_SEGMENT_LENGTH) {
        float segmentFraction = posDelta / MAX_SEGMENT_LENGTH;
        float segmentRemainder = segmentFraction % 1F;
        int numSegments = (int)Mathf.Floor(segmentFraction);
        Vector3 segment = (strokePosition - _lastStrokePointAdded).normalized * MAX_SEGMENT_LENGTH;
        Vector3 curPos = _lastStrokePointAdded;
        float segmentDeltaTime = Time.deltaTime * segmentFraction;
        float remainderDeltaTime = Time.deltaTime * segmentRemainder;
        float curDeltaTime = 0F;
        for (int i = 0; i < numSegments; i++) {
          ProcessAddStrokePoint(curPos + segment, curDeltaTime + segmentDeltaTime);
          curPos += segment;
          curDeltaTime += segmentDeltaTime;
        }
        ProcessAddStrokePoint(strokePosition, curDeltaTime + remainderDeltaTime);
      }
      else {
        ProcessAddStrokePoint(strokePosition, Time.deltaTime);
      }
    }
    else {
      ProcessAddStrokePoint(strokePosition, Time.deltaTime);
    }

  }

  private void ProcessAddStrokePoint(Vector3 point, float effDeltaTime) {
    bool shouldAdd = !_firstStrokePointAdded
      || Vector3.Distance(_lastStrokePointAdded, point)
          >= Mathf.Lerp(MIN_THICKNESS_MIN_SEGMENT_LENGTH, MIN_THICKNESS_MIN_SEGMENT_LENGTH, _thicknessFilter._lastNormalizedValue);

    _timeSinceLastAddition += effDeltaTime;

    if (shouldAdd) {
      StrokePoint strokePoint = new StrokePoint();
      strokePoint.position = point;
      strokePoint.rotation = Quaternion.identity;
      strokePoint.handOrientation = _pinchDetector.Rotation * Quaternion.Euler((_pinchDetector.HandModel.Handedness == Chirality.Left ? leftHandEulerRotation : rightHandEulerRotation));
      strokePoint.deltaTime = _timeSinceLastAddition;

      _strokeProcessor.UpdateStroke(strokePoint);

      _firstStrokePointAdded = true;
      _lastStrokePointAdded = strokePoint.position;
      _timeSinceLastAddition = 0F;
    }
  }

  private void EndStroke() {
    _strokeProcessor.EndStroke();
    _firstStrokePointAdded = false;
  }

  // TODO DELETEME FIXME
  private void DoOnMeshStrokeFinalized(Mesh mesh, List<StrokePoint> stroke) {
    GameObject finishedRibbonMesh = new GameObject();
    finishedRibbonMesh.name = stroke[0].color.r + ", "+stroke[0].color.g + ", "+stroke[0].color.b;
    MeshFilter filter = finishedRibbonMesh.AddComponent<MeshFilter>();
    MeshRenderer renderer = finishedRibbonMesh.AddComponent<MeshRenderer>();
    Material ribbonMat = new Material(Shader.Find("LeapMotion/RibbonShader"));
    ribbonMat.hideFlags = HideFlags.HideAndDontSave;
    renderer.material = ribbonMat;
    filter.mesh = mesh;

    _historyManager.NotifyStroke(finishedRibbonMesh, stroke);

    finishedRibbonMesh.transform.parent = _ribbonParentObject.transform;
  }

  // Used to produce strokes from stroke objects, e.g., when loading scenes.
  public void ShortcircuitStrokeToRenderer(List<StrokePoint> stroke) {
    _ribbonRenderer.InitializeRenderer();
    _ribbonRenderer.RefreshRenderer(stroke, stroke.Count - 1);
    _ribbonRenderer.FinalizeRenderer();
  }

}