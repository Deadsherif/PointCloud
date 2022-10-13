using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.Attributes;


namespace PointCloud
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            List<Solid> targetedsolids = new List<Solid>();
            IList<GeometryObject> points = new List<GeometryObject>();
            IList<CurveLoop> loops = new List<CurveLoop>();
            var x = uidoc.Selection.PickObject(ObjectType.Element);
            var pointCloudInstance = doc.GetElement(x) as PointCloudInstance;
            var geo = pointCloudInstance.get_Geometry(new Options());
            if (pointCloudInstance != null)
            {
                var boundingbox = pointCloudInstance.get_BoundingBox(null);
                var mid = (boundingbox.Max + boundingbox.Min) / 2;
                List<Plane> planes = new List<Plane>();
                List<XYZ> pois = new List<XYZ>();
                var trans = pointCloudInstance.GetTransform();
                //x planes
                planes.Add(Plane.CreateByNormalAndOrigin(XYZ.BasisX, boundingbox.Min));
                planes.Add(Plane.CreateByNormalAndOrigin(-XYZ.BasisX, boundingbox.Max));
                //y planes
                planes.Add(Plane.CreateByNormalAndOrigin(XYZ.BasisY, boundingbox.Min));
                planes.Add(Plane.CreateByNormalAndOrigin(-XYZ.BasisY, boundingbox.Max));
                //z planes
                planes.Add(Plane.CreateByNormalAndOrigin(XYZ.BasisZ, boundingbox.Min));
                planes.Add(Plane.CreateByNormalAndOrigin(-XYZ.BasisZ, boundingbox.Max));
                PointCloudFilter pcf = PointCloudFilterFactory.CreateMultiPlaneFilter(planes);
                var cpoints = pointCloudInstance.GetPoints(pcf, .0001,200000);
                foreach (var point in cpoints)
                {
                    //*3.2808399
                    var targetpoint = new XYZ(point.X , point.Y , point.Z);
                    var newpoint = TransformPoint(targetpoint, trans);
                    points.Add(Point.Create(newpoint) as GeometryObject);
                    pois.Add(newpoint);


                }
                Transaction tr = new Transaction(doc, "create geo");
                tr.Start();
                ElementId categoryId = new ElementId(BuiltInCategory.OST_GenericModel);
                IList<GeometryObject> objects = new List<GeometryObject>();
               var orderedpoint =  pois.OrderBy(p => p.X);
                foreach (var point in orderedpoint)
                {
                   var solid =  CreateSphereSolid(doc, point);
                   var Object = solid as GeometryObject;
                   objects.Add(Object);
                }
                //var OurFinalSolid = JoinSolids(targetedsolids);
                //create direct shape and assign the sphere shape
                DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                ds.ApplicationId = "Application id";
                ds.ApplicationDataId = "Geometry object id";
                //ds.SetShape(new GeometryObject[] { OurFinalSolid });
                ds.SetShape(objects);
                //geometry =  ds.get_Geometry(new Options());
                //var OurFinalSolid = JoinSolids(geometry);
                //ds.SetShape(new List<GeometryObject> { OurFinalSolid });
                var directshape = new FilteredElementCollector(doc).OfCategoryId(categoryId).WhereElementIsNotElementType().ToElements();
                   var pointcloudgeo = directshape.FirstOrDefault() as  Element;
                var StructuralElements = GetStructuralElements(doc).WhereElementIsNotElementType().ToList();
                tr.Commit();
                #region old code

                //Transaction rr = new Transaction(doc, "new ");
                //rr.Start();
                //    var walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().ToElements();
                //var cols = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().ToElements();
                //var beams = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().ToElements();
                //var foundation = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().ToElements();
                //var floors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType().ToElements();

                //List<Element> compelements = new List<Element>();
                //compelements.AddRange(walls);
                //compelements.AddRange(cols);
                //compelements.AddRange(beams);
                //compelements.AddRange(foundation);
                //compelements.AddRange(floors);

                //compelements.AddRange(directshape);

                //var bb = pointcloudgeo.get_BoundingBox(null);
                //Outline outline = new Outline(bb.Min, bb.Max);
                //BoundingBoxIsInsideFilter filter = new BoundingBoxIsInsideFilter(outline);
                //var inter = new FilteredElementCollector(doc).WherePasses(filter).WhereElementIsNotElementType().ToList();
                //var flag = inter.Count();
                //TaskDialog.Show("Elements", (compelements.Count-flag).ToString());
                //rr.Commit();

                #endregion
            }




            return Result.Succeeded;
        }
        public Document GetLinkedDocument(Document doc)
        {
            var linkInstance = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks).WhereElementIsNotElementType().FirstOrDefault() as RevitLinkInstance;
            var Linkeddoc = linkInstance.GetLinkDocument();
            return Linkeddoc;
        }
        FilteredElementCollector GetStructuralElements(Document doc)
        {
            // what categories of family instances
            // are we interested in?

            BuiltInCategory[] bics = new BuiltInCategory[] {
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralFoundation
                                                            };

            IList<ElementFilter> a
              = new List<ElementFilter>(bics.Count());

            foreach (BuiltInCategory bic in bics)
            {
                a.Add(new ElementCategoryFilter(bic));
            }

            LogicalOrFilter categoryFilter
              = new LogicalOrFilter(a);

            LogicalAndFilter familyInstanceFilter
              = new LogicalAndFilter(categoryFilter,
                new ElementClassFilter(
                  typeof(FamilyInstance)));

            IList<ElementFilter> b
              = new List<ElementFilter>(6);

            b.Add(new ElementClassFilter(
              typeof(Wall)));

            b.Add(new ElementClassFilter(
              typeof(Floor)));


            b.Add(familyInstanceFilter);

            LogicalOrFilter classFilter
              = new LogicalOrFilter(b);

            FilteredElementCollector collector
              = new FilteredElementCollector(doc);

            collector.WherePasses(classFilter);

            return collector;
        }
        public Solid JoinSolids(GeometryElement geometryElement)
        {
            Solid JoinedSolid = null;
            List<Solid> healthySolids = new List<Solid>();
            foreach (Solid solid in geometryElement)
            {
                if (null != solid && 0 < solid.Faces.Size)
                {
                    var newsolid = SolidUtils.Clone(solid);
                    healthySolids.Add(newsolid);

                }
            }
            if (healthySolids.Count > 1)
            {
                for (int i = 1; i < healthySolids.Count; i++)
                {
                    if (JoinedSolid == null)
                    {
                        JoinedSolid = healthySolids[0];
                    }

                    JoinedSolid = BooleanOperationsUtils.ExecuteBooleanOperation(JoinedSolid, healthySolids[i], BooleanOperationsType.Union);

                }

            }
           
            return JoinedSolid;
        }
        public static IList<Mesh> GetTargetSolids(Element element)
        {
            List<Mesh> meshes = new List<Mesh>();


            Options options = new Options();
            options.DetailLevel = ViewDetailLevel.Fine;
            GeometryElement geomElem = element.get_Geometry(options);
            
            foreach (GeometryObject geomObj in geomElem)
            {
                meshes.Add(geomObj as Mesh);    
            }
            return meshes;
        }
        public static XYZ TransformPoint(XYZ point, Transform transform)
        {
            double x = point.X;
            double y = point.Y;
            double z = point.Z;

            //transform basis of the old coordinate system in the new coordinate // system
            XYZ b0 = transform.get_Basis(0);
            XYZ b1 = transform.get_Basis(1);
            XYZ b2 = transform.get_Basis(2);
            XYZ origin = transform.Origin;

            //transform the origin of the old coordinate system in the new 
            //coordinate system
            double xTemp = x * b0.X + y * b1.X + z * b2.X + origin.X;
            double yTemp = x * b0.Y + y * b1.Y + z * b2.Y + origin.Y;
            double zTemp = x * b0.Z + y * b1.Z + z * b2.Z + origin.Z;

            return new XYZ(xTemp, yTemp, zTemp);
        }
        public Solid CreateSphereSolid(Document doc,XYZ center)
        {
            List<Curve> profile = new List<Curve>();
            Solid sphere = null;
            // first create sphere with 2' radius
            
            double radius = 0.75;
            XYZ profile00 = center;
            XYZ profilePlus = center + new XYZ(0, radius, 0);
            XYZ profileMinus = center - new XYZ(0, radius, 0);

            profile.Add(Line.CreateBound(profilePlus, profileMinus));
            profile.Add(Arc.Create(profileMinus, profilePlus, center + new XYZ(radius, 0, 0)));

            CurveLoop curveLoop = CurveLoop.Create(profile);
            SolidOptions options = new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId);

            Frame frame = new Frame(center, XYZ.BasisX, -XYZ.BasisZ, XYZ.BasisY);
            if (Frame.CanDefineRevitGeometry(frame) == true)
            {
                 sphere = GeometryCreationUtilities.CreateRevolvedGeometry(frame, new CurveLoop[] { curveLoop }, 0, 2 * Math.PI, options);

                ////create direct shape and assign the sphere shape
                //DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));

                //ds.ApplicationId = "Application id";
                //ds.ApplicationDataId = "Geometry object id";
                //ds.SetShape(new GeometryObject[] { sphere });

            }
                return sphere;
        }
        public static ElementId CreateNewMaterial(Document doc, Byte R, Byte G, Byte B, int A)//alpha channel to be added
        {
            string materialName = $"InsulationMaterial {R}-{G}-{B}-{A}";
            Material _mat = null;
            FilteredElementCollector fec = new FilteredElementCollector(doc);
            _mat = fec.OfCategory(BuiltInCategory.OST_Materials).Where(m => m.Name == materialName).FirstOrDefault() as Material;
            if (_mat == null)
            {
                ElementId matID = Material.Create(doc, materialName);
                Material mat = doc.GetElement(matID) as Material;
                mat.Color = new Color(R, G, B);
                int alpha = A > 100 ? 100 : A;
                alpha = alpha < 0 ? 0 : alpha;
                mat.Transparency = alpha;
                return matID;
            }
            else
            {
                return _mat.Id;
            }
        }
    }
}
