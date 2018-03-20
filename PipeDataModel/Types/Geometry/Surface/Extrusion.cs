﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PipeDataModel.Types.Geometry;

namespace PipeDataModel.Types.Geometry.Surface
{
    public class Extrusion: Surface
    {
        #region fields
        private Curve.Curve _profile;
        private Vec _direction;
        private double _height;
        #endregion

        #region properties
        public Curve.Curve ProfileCurve { get => _profile; set => _profile = value; }
        public Vec Direction { get => _direction; set => _direction = value.Unitized; }
        public double Height { get => _height; set => _height = value; }
        #endregion

        #region constructors
        public Extrusion(Curve.Curve profile, Vec dir, double height)
        {
            _profile = profile;
            Direction = dir;
            _height = height;
        }
        #endregion

        #region methods
        public override bool Equals(IPipeMemberType other)
        {
            if (!GetType().IsAssignableFrom(other.GetType())) { return false; }
            Extrusion otherExt = (Extrusion)other;
            return _profile.Equals(otherExt.ProfileCurve) && _direction.Equals(otherExt.Direction) && _height == otherExt.Height;
        }
        #endregion
    }
}
