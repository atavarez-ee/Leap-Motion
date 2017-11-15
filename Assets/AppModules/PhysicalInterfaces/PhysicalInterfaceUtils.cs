﻿

using UnityEngine;

namespace Leap.Unity.PhysicalInterfaces {

  public class PhysicalInterfaceUtils {

    #region Constants

    /// <summary>
    /// The minimum speed past which a released object should be considered thrown,
    /// and beneath which a released object should be considered placed.
    /// </summary>
    public const float MIN_THROW_SPEED = 0.70f;
    public const float MIN_THROW_SPEED_SQR = MIN_THROW_SPEED * MIN_THROW_SPEED;

    /// <summary>
    /// For the purposes of mapping values based on throw speed, 10 m/s represents
    /// about a quarter of the speed of the world's fastest fastball.
    /// </summary>
    public const float MID_THROW_SPEED = 10.00f;

    /// <summary>
    /// For the purposes of mapping values based on throw speed, 40 m/s is about the
    /// speed of the fastest fast-ball. (~90 mph.)
    /// </summary>
    public const float MAX_THROW_SPEED = 40.00f;

    /// <summary>
    /// A standard speed for calculating e.g. how much time it should take for an
    /// element to move a given distance.
    /// </summary>
    public const float STANDARD_SPEED = 1.00f;

    /// <summary>
    /// A distance representing being well within arms-reach without being too close to
    /// the head.
    /// </summary>
    public const float OPTIMAL_UI_DISTANCE = 0.60f;

    #endregion

    #region 

    public static Pose SmoothMove(Pose prev, Pose current, Pose target) {
      var prevSqrDist = (current.position - prev.position).sqrMagnitude;
      var lerpFilter = prevSqrDist.Map(0.0f, 0.4f, 0.2f, 1f);

      var prevAngle = Quaternion.Angle(current.rotation, prev.rotation);
      var slerpFilter = prevAngle.Map(0.0f, 16f, 0.01f, 1f);

      var sqrDist = (target.position - current.position).sqrMagnitude;
      float angle = Quaternion.Angle(current.rotation, target.rotation);

      var smoothedPose = new Pose(Vector3.Lerp(current.position, target.position,
                                   sqrDist.Map(0.00001f, 0.0004f,
                                               0.2f, 0.8f) * lerpFilter),
                                  Quaternion.Slerp(current.rotation,
                                    target.rotation,
                                    angle.Map(0.3f, 4f,
                                              0.01f, 0.8f) * slerpFilter));

      return smoothedPose;
    }

    #endregion

  }

}