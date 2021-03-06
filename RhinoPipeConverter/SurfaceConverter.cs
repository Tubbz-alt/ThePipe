﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PipeDataModel.Types;
using rh = Rhino.Geometry;
using pp = PipeDataModel.Types.Geometry;
using pps = PipeDataModel.Types.Geometry.Surface;
using ppc = PipeDataModel.Types.Geometry.Curve;

namespace RhinoPipeConverter
{
    public class SurfaceConverter : PipeConverter<rh.Surface, pps.Surface>
    {
        public SurfaceConverter(CurveConverter curveConv, Vector3DConverter vecConv, Point3dConverter ptConv)
        {
            //extrusion surfaces
            AddConverter(new PipeConverter<rh.Extrusion, pps.Extrusion>(
                (rhE) => {
                    ppc.Line path = (ppc.Line)curveConv.ConvertToPipe<rh.Curve, ppc.Curve>(rhE.PathLineCurve());
                    
                    pps.Extrusion extr = new pps.Extrusion(curveConv.ConvertToPipe<rh.Curve, ppc.Curve>(rhE.Profile3d(0, 0)),
                        vecConv.ConvertToPipe<rh.Vector3d, pp.Vec>(rhE.PathTangent), path.Length);

                    for (int i = 1; i < rhE.ProfileCount; i++)
                    {
                        extr.Holes.Add(curveConv.ConvertToPipe<rh.Curve, ppc.Curve>(rhE.Profile3d(i, 0)));
                    }

                    extr.CappedAtStart = rhE.IsCappedAtBottom;
                    extr.CappedAtEnd = rhE.IsCappedAtTop;
                    extr.SurfaceNormal = vecConv.ToPipe<rh.Vector3d, pp.Vec>(rhE.NormalAt(rhE.Domain(0).Mid, rhE.Domain(1).Mid));

                    return extr;
                },
                (ppE) => {
                    if(1 - ppE.Direction.Dot(new pp.Vec(0, 0, 1)) > 1e-3)
                    {
                        //the extrusion is not vertical
                        throw new InvalidOperationException("Cannot create this extrusion. " +
                            "Try converting it into a polysurface and pushing it again");
                    }
                    var profile = curveConv.FromPipe<rh.Curve, ppc.Curve>(ppE.ProfileCurve);
                    rh.Extrusion extr = rh.Extrusion.Create(profile, ppE.Height, ppE.CappedAtEnd||ppE.CappedAtStart);
                    ppE.Holes.ForEach((h) => extr.AddInnerProfile(curveConv.FromPipe<rh.Curve, ppc.Curve>(h)));
                    //extr.SetOuterProfile(profile, false);
                    //extr.SetPathAndUp(profile.PointAtStart, profile.PointAtStart + pathVec, pathVec);

                    string msg;
                    if(!extr.IsValidWithLog(out msg))
                    {
                        System.Diagnostics.Debug.WriteLine(msg);
                        throw new InvalidOperationException("Cannot create a valid extrusion from the received data: \n" + msg);
                    }

                    var rhNorm = extr.NormalAt(extr.Domain(0).Mid, extr.Domain(1).Mid);
                    if(rh.Vector3d.Multiply(rhNorm, vecConv.FromPipe<rh.Vector3d, pp.Vec>(ppE.SurfaceNormal)) < 0)
                    {
                        //extrusions don't need to be flipped;
                    }

                    return extr;
                }
            ));

            //NurbsSurfaces
            AddConverter(new PipeConverter<rh.NurbsSurface, pps.NurbsSurface>(
                (rns) =>
                {
                    pps.NurbsSurface nurbs = new pps.NurbsSurface(rns.Points.CountU, rns.Points.CountV, rns.Degree(0), rns.Degree(1));
                    
                    for (int u = 0; u < rns.Points.CountU; u++)
                    {
                        for (int v = 0; v < rns.Points.CountV; v++)
                        {
                            nurbs.SetControlPoint(ptConv.ToPipe<rh.Point3d, pp.Vec>(rns.Points.GetControlPoint(u, v).Location), u, v);
                            nurbs.SetWeight(rns.Points.GetControlPoint(u, v).Weight, u, v);
                        }
                    }
                    rh.Interval uDomain = rns.Domain(0);
                    rh.Interval vDomain = rns.Domain(1);
                    Func<double, rh.Interval, double> scaleKnot = (k, domain) => (k - domain.Min) / (domain.Length);
                    nurbs.UKnots = rns.KnotsU.Select((k) => scaleKnot.Invoke(k, uDomain)).ToList();
                    nurbs.VKnots = rns.KnotsV.Select((k) => scaleKnot.Invoke(k, vDomain)).ToList();

                    nurbs.IsClosedInU = rns.IsClosed(0);
                    nurbs.IsClosedInV = rns.IsClosed(1);

                    nurbs.SurfaceNormal = vecConv.ToPipe<rh.Vector3d, pp.Vec>(rns.NormalAt(rns.Domain(0).Mid, rns.Domain(1).Mid));

                    return nurbs;
                },
                (pns) => {
                    if (pns.IsClosedInU) { pns.WrapPointsToCloseSurface(0); }
                    if (pns.IsClosedInV) { pns.WrapPointsToCloseSurface(1); }

                    var nurbs = rh.NurbsSurface.Create(3, true, pns.UDegree + 1, pns.VDegree + 1, pns.UCount, pns.VCount);
                    
                    for (int u = 0; u < pns.UCount; u++)
                    {
                        for (int v = 0; v < pns.VCount; v++)
                        {
                            var cp = new rh.ControlPoint(ptConv.FromPipe<rh.Point3d, pp.Vec>(pns.GetControlPointAt(u, v)), 
                                pns.GetWeightAt(u,v));
                            nurbs.Points.SetControlPoint(u, v, cp);
                        }
                    }

                    rh.Interval uDomain = nurbs.Domain(0);
                    rh.Interval vDomain = nurbs.Domain(1);
                    Func<double, rh.Interval, double> scaleKnot = (k, domain) => k * (domain.Length) + domain.Min;
                    if(nurbs.KnotsU.Count == pns.UKnots.Count)
                    {
                        for(int i = 0; i < nurbs.KnotsU.Count; i++)
                        {
                            nurbs.KnotsU[i] = scaleKnot.Invoke(pns.UKnots[i], uDomain);
                        }
                    }
                    if (nurbs.KnotsV.Count == pns.VKnots.Count)
                    {
                        for (int i = 0; i < nurbs.KnotsV.Count; i++)
                        {
                            nurbs.KnotsV[i] = scaleKnot.Invoke(pns.VKnots[i], vDomain);
                        }
                    }

                    string msg;
                    if(!nurbs.IsValidWithLog(out msg))
                    {
                        System.Diagnostics.Debug.WriteLine(msg);
                        if (!nurbs.IsPeriodic(0)) { nurbs.KnotsU.CreateUniformKnots(1.0 / (nurbs.Points.CountU)); }
                        else { nurbs.KnotsU.CreatePeriodicKnots(1.0 / (nurbs.Points.CountU)); }
                        if (!nurbs.IsPeriodic(1)) { nurbs.KnotsV.CreateUniformKnots(1.0 / (nurbs.Points.CountV)); }
                        else { nurbs.KnotsV.CreatePeriodicKnots(1.0 / (nurbs.Points.CountV)); }

                        if (!nurbs.IsValid) { throw new InvalidOperationException("Cannot create a valid NURBS surface: \n" + msg); }
                    }

                    var rhNorm = nurbs.NormalAt(nurbs.Domain(0).Mid, nurbs.Domain(1).Mid);
                    if (rh.Vector3d.Multiply(rhNorm, vecConv.FromPipe<rh.Vector3d, pp.Vec>(pns.SurfaceNormal)) < 0)
                    {
                        //need not flip rhino surfaces
                    }

                    return nurbs;
                }
            ));
        }
    }
}
