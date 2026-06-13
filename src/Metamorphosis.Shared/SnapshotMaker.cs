using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Globalization;

namespace Metamorphosis
{
    internal class SnapshotMaker
    {
        private Document _doc;
        private Dictionary<long, Parameter> _paramDict = new Dictionary<long, Parameter>();
        private Dictionary<string, int> _valueDict = new Dictionary<string, int>();
        // cache GetParameters results so each element is queried exactly once
        private Dictionary<long, IList<Parameter>> _elemParamCache = new Dictionary<long, IList<Parameter>>();
        // cache AsValueString results per (paramId, rawValue) — avoids re-entering Revit's
        // formatting pipeline for parameters where many elements share the same raw value
        private Dictionary<long, Dictionary<long, string>> _paramValueCache = new Dictionary<long, Dictionary<long, string>>();
        private Dictionary<string, string> _headerDict = new Dictionary<string, string>();
        private string _filename;
        private string _dbFilename;
        private int _valueId = 0;
        private List<Level> _allLevels;
        private Utilities.Settings.LogLevel _logLevel = Utilities.Settings.LogLevel.Basic;


        #region Constructor
        internal SnapshotMaker(Document doc, string filename)
        {
            _doc = doc;
            _filename = filename;

            _dbFilename = _filename;
            // see: http://system.data.sqlite.org/index.html/info/bbdda6eae2
            if (_filename.StartsWith(@"\\")) _dbFilename = @"\\" + _dbFilename;

            _logLevel = Utilities.Settings.GetLogLevel();
        }
        #endregion

        #region Accessor
        internal TimeSpan Duration { get; private set; }
        #endregion

        #region PublicMethods
        internal void Export()
        {
            // make the sqlite database
            createDatabase();
            // populate
            exportParameterData();

            _doc.Application.WriteJournalComment("Export Completed. Releasing hold on database file.", false);
            // release the hold on the database 
            //https://stackoverflow.com/questions/8511901/system-data-sqlite-close-not-releasing-database-file
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        #endregion

        #region PrivateMethods
        private void createDatabase()
        {
            _doc.Application.WriteJournalComment("Creating database: " + _filename, false);
            if (File.Exists(_filename)) File.Delete(_filename); // we have to replace the contents anyway

            // ran into a case where the path didn't exist. make it happen.
            string folder = Path.GetDirectoryName(_filename);
            if (Directory.Exists(folder) == false) Directory.CreateDirectory(folder);

            //create the SQLite database file.
            SQLiteConnection.CreateFile(_filename);

            using (SQLiteConnection conn = new SQLiteConnection("Data Source=" + _dbFilename + ";Version=3;"))
            {
                conn.Open();
                string prefix = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;


                // create the table structure from the sql instructions.
                string[] lines = Utilities.DataUtility.ReadSQLScript($"{prefix}.databaseFormat.txt");

                foreach (string sql in lines)
                {

                    SQLiteCommand command = new SQLiteCommand(sql, conn);
                    command.ExecuteNonQuery();
                }
            }
        
        }

        
        private void exportParameterData()
        {
            _doc.Application.WriteJournalComment("Retrieving Data...", false);
            DateTime start = DateTime.Now;
            
            // retrieve all of the instance elements, and process them.
            FilteredElementCollector coll = new FilteredElementCollector(_doc);
            coll.WhereElementIsNotElementType();

            Dictionary<ElementId, Element> typeElementsUsed = new Dictionary<ElementId, Element>();
            IList<Element> instances = coll.ToElements().Where(e => e.Category != null).ToList();
            foreach ( var elem in instances)
            {
                if (elem.Category == null) continue; // don't do it!

                // see if the current element has a type element, and make sure we're getting that.
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    if (typeElementsUsed.ContainsKey(typeId) == false)
                    {
                        Element typeElem = _doc.GetElement(typeId);
                        if ((typeElem.Category != null))
                        {
                            typeElementsUsed.Add(typeId, typeElem); // only add if it's a typeElement with Category
                        }
                    }
                }
            }

            string msg = (DateTime.Now - start) + ": " + instances.Count + " instances and " + typeElementsUsed.Count + " types.";
            _doc.Application.WriteJournalComment(msg, false);
            System.Diagnostics.Debug.WriteLine(msg);

            // go through all of the type elements and instances and capture the parameter ids
            _headerDict["SchemaVersion"] = "1.1";
            _headerDict["Model"] = Utilities.RevitUtils.GetModelPath(_doc);
            _headerDict["ExportVersion"] = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            _headerDict["ExportDate"] = DateTime.Now.ToString();
            _headerDict["ExportDateTicks"] = DateTime.Now.Ticks.ToString();
            _headerDict["ExportingUser"] = Environment.UserDomainName + "\\" + Environment.UserName;
            _headerDict["MachineName"] = Environment.MachineName;
            _headerDict["RevitVersion"] = _doc.Application.VersionNumber;
            _headerDict["RevitBuild"] = _doc.Application.VersionBuild;

#if REVIT2015 || REVIT2016 || REVIT2017 || REVIT2018
                // do not support Document Version
#else
            DocumentVersion ver = Document.GetDocumentVersion(_doc);
            _headerDict["DocumentGuid"] = ver.VersionGUID.ToString();
            _headerDict["NumSaves"] = ver.NumberOfSaves.ToString();
#endif

            updateHeaderTable();

            updateParameterDictionary(typeElementsUsed.Values.ToList());
            log((DateTime.Now - start) + ": Parameter Dictionary Updated for Types");
            updateParameterDictionary(instances);
            log((DateTime.Now - start) + ": Parameter Dictionary Updated for Instances");

            updateIdTable(typeElementsUsed.Values.ToList(), true);
            log((DateTime.Now - start) + ": Id Table Updated for Types");
            updateIdTable(instances, false);
            log((DateTime.Now - start) + ": Id Table Updated for Instances");
            updateAttributeTable();
            log((DateTime.Now - start) + ": Attribute Table Updated for All");


            updateEntityAttributeValues(typeElementsUsed.Values.ToList());
            log((DateTime.Now - start) + ": Att/Values Table Updated for Types");

            updateEntityAttributeValues(instances);
            log((DateTime.Now - start) + ": Att/Values Table Updated for Instances");
            updateValueTable();
            log((DateTime.Now - start) + ": Value Table Updated for All");

            updateGeometryTable(instances);
            log((DateTime.Now - start) + ": Geometry Table Updated for Types");
            Duration = DateTime.Now - start;
            log("Total Time: " + Duration.TotalMinutes + " minutes");
        }

        // Returns the display string for a parameter value.
        // For non-string types the raw value (int/double/elementId) is used as a cache key
        // so AsValueString() — which crosses the managed/native boundary and runs Revit's
        // full unit-conversion and localization pipeline — is called at most once per
        // unique raw value per parameter instead of once per element.
        private string GetOrFormatParameterValue(Parameter p)
        {
            if (p.StorageType == StorageType.String) return p.AsString();

            long rawKey;
            switch (p.StorageType)
            {
                case StorageType.Integer:
                    rawKey = p.AsInteger();
                    break;
                case StorageType.Double:
                    rawKey = BitConverter.DoubleToInt64Bits(p.AsDouble());
                    break;
                case StorageType.ElementId:
                    rawKey = p.AsElementId().AsLong();
                    break;
                default:
                    return p.AsValueString();
            }

            long paramId = p.Id.AsLong();
            if (!_paramValueCache.TryGetValue(paramId, out var inner))
                _paramValueCache[paramId] = inner = new Dictionary<long, string>();

            if (!inner.TryGetValue(rawKey, out string formatted))
                inner[rawKey] = formatted = p.AsValueString();

            return formatted;
        }

        // Speed up bulk inserts into a freshly-created file. The snapshot is always
        // rebuilt from scratch, so crash-durability guarantees are unnecessary.
        private static void ApplyWritePragmas(SQLiteConnection conn)
        {
            foreach (string pragma in new[] {
                "PRAGMA synchronous = OFF",
                "PRAGMA journal_mode = MEMORY",
                "PRAGMA temp_store = MEMORY",
                "PRAGMA cache_size = -65536",   // 64 MB page cache
                "PRAGMA locking_mode = EXCLUSIVE",
            })
                new SQLiteCommand(pragma, conn).ExecuteNonQuery();
        }

        private void log(string msg)
        {
            _doc.Application.WriteJournalComment(msg, false);
            System.Diagnostics.Debug.WriteLine(msg);
        }

        private void updateIdTable(IList<Element> elements, bool isTypes)
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=" + _dbFilename + ";Version=3;"))
            {
                try
                {
                    conn.Open();
                    ApplyWritePragmas(conn);
                    using (var transaction = conn.BeginTransaction())
                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO _objects_id (id,external_id,category,isType,versionguid) VALUES(@id,@external_id,@category,@isType,@versionguid)", conn))
                    {
                        var pId    = cmd.Parameters.Add("@id",           DbType.Int64);
                        var pExtId = cmd.Parameters.Add("@external_id",  DbType.String);
                        var pCat   = cmd.Parameters.Add("@category",     DbType.String);
                        var pType  = cmd.Parameters.Add("@isType",       DbType.Int32);
                        var pVer   = cmd.Parameters.Add("@versionguid",  DbType.String);
                        cmd.Prepare();

                        foreach (Element e in elements)
                        {
                            string versionGuid = null;
#if REVIT2015 || REVIT2016 || REVIT2017 || REVIT2018 || REVIT2019 || REVIT2020
                            // we do nothing
#else
                            if (e.VersionGuid != Guid.Empty) versionGuid = e.VersionGuid.ToString();
#endif
                            Category c = e.Category;
                            if (c == null)
                            {
                                FamilySymbol fs = e as FamilySymbol;
                                if (fs != null) c = fs.Family.FamilyCategory;
                            }
                            pId.Value    = e.Id.AsLong();
                            pExtId.Value = e.UniqueId;
                            pCat.Value   = (c != null) ? c.Name : "(none)";
                            pType.Value  = isTypes ? 1 : 0;
                            pVer.Value   = (object)versionGuid ?? DBNull.Value;
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    log("Exception updating ID Table: " + ex.GetType().Name + ": " + ex.Message);
                    throw;
                }
            }
        }

        private void updateHeaderTable()
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=" + _dbFilename + ";Version=3;"))
            {
                try
                {
                    conn.Open();
                    ApplyWritePragmas(conn);
                    using (var transaction = conn.BeginTransaction())
                    using (var cmd = new SQLiteCommand("INSERT INTO _objects_header (keyword,value) VALUES(@keyword,@value)", conn))
                    {
                        var pKey = cmd.Parameters.Add("@keyword", DbType.String);
                        var pVal = cmd.Parameters.Add("@value",   DbType.String);
                        cmd.Prepare();
                        foreach (var pair in _headerDict)
                        {
                            pKey.Value = pair.Key;
                            pVal.Value = pair.Value;
                            cmd.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    log("Exception updating header Table: " + ex.GetType().Name + ": " + ex.Message);
                    throw;
                }
            }
        }
        private void updateAttributeTable()
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=" + _dbFilename + ";Version=3;"))
            {
                try
                {
                    conn.Open();
                    ApplyWritePragmas(conn);
                    using (var transaction = conn.BeginTransaction())
                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO _objects_attr (id,name,category,data_type) VALUES(@id,@name,@category,@data_type)", conn))
                    {
                        var pId       = cmd.Parameters.Add("@id",        DbType.Int64);
                        var pName     = cmd.Parameters.Add("@name",      DbType.String);
                        var pCategory = cmd.Parameters.Add("@category",  DbType.String);
                        var pDataType = cmd.Parameters.Add("@data_type", DbType.Int32);
                        pDataType.Value = -1;
                        cmd.Prepare();

                        foreach (var pair in _paramDict)
                        {
                            string name = pair.Value.Definition.Name;
#if REVIT2015 || REVIT2016 || REVIT2017 || REVIT2018 || REVIT2019 || REVIT2020 || REVIT2021 || REVIT2022 || REVIT2023
                            var group = LabelUtils.GetLabelFor(pair.Value.Definition.ParameterGroup);
#else
                            var group = LabelUtils.GetLabelForGroup(pair.Value.Definition.GetGroupTypeId());
#endif
                            pId.Value       = pair.Value.Id.AsLong();
                            pName.Value     = name;
                            pCategory.Value = group;
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    log("Exception updating attr Table: " + ex.GetType().Name + ": " + ex.Message);
                    throw;
                }
            }
        }

        private void updateValueTable()
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=" + _dbFilename + ";Version=3;"))
            {
                try
                {
                    conn.Open();
                    ApplyWritePragmas(conn);
                    using (var transaction = conn.BeginTransaction())
                    using (var cmd = new SQLiteCommand("INSERT INTO _objects_val (id,value) VALUES(@id,@value)", conn))
                    {
                        var pId  = cmd.Parameters.Add("@id",    DbType.Int32);
                        var pVal = cmd.Parameters.Add("@value", DbType.String);
                        cmd.Prepare();
                        foreach (var pair in _valueDict)
                        {
                            pId.Value  = pair.Value;
                            pVal.Value = pair.Key;
                            cmd.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    log("Exception updating Value Table: " + ex.GetType().Name + ": " + ex.Message);
                    throw;
                }
            }
        }

        private void updateEntityAttributeValues(IList<Element> elems)
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=" + _dbFilename + ";Version=3;"))
            {
                try
                {
                    conn.Open();
                    ApplyWritePragmas(conn);
                    using (var transaction = conn.BeginTransaction())
                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO _objects_eav (entity_id,attribute_id,value_id) VALUES(@entity_id,@attribute_id,@value_id)", conn))
                    {
                        var pEntityId = cmd.Parameters.Add("@entity_id",    DbType.Int64);
                        var pAttrId   = cmd.Parameters.Add("@attribute_id", DbType.Int64);
                        var pValueId  = cmd.Parameters.Add("@value_id",     DbType.Int32);
                        cmd.Prepare();

                        foreach (Element e in elems)
                        {
                            // use cached parameters from updateParameterDictionary — avoids a second Revit API round-trip
                            IList<Parameter> parms = _elemParamCache.TryGetValue(e.Id.AsLong(), out var cached)
                                ? cached
                                : Utilities.RevitUtils.GetParameters(e);

                            long entityId = e.Id.AsLong();
                            foreach (var p in parms)
                            {
                                if (p.Definition == null) continue;

                                string val = GetOrFormatParameterValue(p);
                                if (val == null) val = "(n/a)";

                                if (!_valueDict.ContainsKey(val))
                                {
                                    _valueId++;
                                    _valueDict.Add(val, _valueId);
                                }

                                pEntityId.Value = entityId;
                                pAttrId.Value   = p.Id.AsLong();
                                pValueId.Value  = _valueDict[val];
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    log("Exception updating EAV Table: " + ex.GetType().Name + ": " + ex.Message);
                    throw;
                }
            }
        }
        private void updateParameterDictionary(IList<Element> elems)
        {
            foreach (Element e in elems)
            {
                IList<Parameter> parms = Utilities.RevitUtils.GetParameters(e);
                _elemParamCache[e.Id.AsLong()] = parms; // cache so updateEntityAttributeValues skips the API call
                foreach (Parameter p in parms)
                {
                    if (p.Definition == null) continue;
                    if (!_paramDict.ContainsKey(p.Id.AsLong())) _paramDict.Add(p.Id.AsLong(), p);
                }
            }
        }


     

        private void updateGeometryTable(IList<Element> elements)
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=" + _dbFilename + ";Version=3;"))
            {
                conn.Open();
                ApplyWritePragmas(conn);
                using (var transaction = conn.BeginTransaction())
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO _objects_geom (id,BoundingBoxMin,BoundingBoxMax,Location,Location2,Level,Rotation) VALUES(@id,@BoundingBoxMin,@BoundingBoxMax,@Location,@Location2,@Level,@Rotation)", conn))
                {
                    var pId   = cmd.Parameters.Add("@id",           DbType.Int64);
                    var pBbMn = cmd.Parameters.Add("@BoundingBoxMin", DbType.String);
                    var pBbMx = cmd.Parameters.Add("@BoundingBoxMax", DbType.String);
                    var pLoc  = cmd.Parameters.Add("@Location",     DbType.String);
                    var pLoc2 = cmd.Parameters.Add("@Location2",    DbType.String);
                    var pLev  = cmd.Parameters.Add("@Level",        DbType.String);
                    var pRot  = cmd.Parameters.Add("@Rotation",     DbType.Single);
                    cmd.Prepare();

                    foreach (Element e in elements)
                    {
                        BoundingBoxXYZ box = e.get_BoundingBox(null);
                        Location loc = e.Location;

                        if ((loc == null) && (box == null)) continue; // nothing to see here.

                        String bbMin = String.Empty;
                        String bbMax = String.Empty;
                        string lp = String.Empty;
                        string lp2 = String.Empty;
                        float rotation = -1.0f;

                        if (box != null)
                        {
                            bbMin = Utilities.RevitUtils.SerializePoint(box.Min);
                            bbMax = Utilities.RevitUtils.SerializePoint(box.Max);
                        }

                        XYZ p1 = null;
                        if (loc != null)
                        {
                            LocationPoint pt = loc as LocationPoint;
                            if (pt != null)
                            {
                                try
                                {
                                    // noted a time where with a group it didn't work.

                                    XYZ pt1 = pt.Point;
                                    // special cases.
                                    if (e.Category.Id.IsCategory(BuiltInCategory.OST_Columns) ||
                                        e.Category.Id.IsCategory(BuiltInCategory.OST_StructuralColumns))
                                    {
                                        // in this case, get the Z value from the 
                                        var offset = e.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);

                                        if ((e.LevelId != ElementId.InvalidElementId) && (offset != null))
                                        {
                                            Level levPt1 = lookupLevel(e, pt1);
                                            double newZ = levPt1.Elevation + offset.AsDouble();
                                            pt1 = new XYZ(pt1.X, pt1.Y, newZ);
                                        }
                                    }

                                    lp = Utilities.RevitUtils.SerializePoint(pt1);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine("Error: " + e.Name + ": " + e.GetType().Name + ": " + ex.Message);
                                }
                                try
                                {
                                    if (e is FamilyInstance)
                                    {
                                        if ((e as FamilyInstance).CanRotate)
                                        {
                                            rotation = (float)pt.Rotation;
                                        }
                                    }
                                }
                                catch {  // swallow. Some just don't like it...
                                }
                            }
                            else
                            {
                                LocationCurve crv = loc as LocationCurve;
                                if (crv != null)
                                {
                                    if (crv.Curve.IsBound)
                                    {
                                        p1 = crv.Curve.GetEndPoint(0);
                                        XYZ p2 = crv.Curve.GetEndPoint(1);
                                        lp = Utilities.RevitUtils.SerializePoint(p1);
                                        lp2 = Utilities.RevitUtils.SerializePoint(p2);
                                    }
                                }
                                else
                                {
                                    if (box == null)
                                    {
                                        // ok, special case one: Grid
                                        if (e is Grid)
                                        {
                                            Grid g = e as Grid;
                                            p1 = g.Curve.GetEndPoint(0);
                                            XYZ p2 = g.Curve.GetEndPoint(1);
                                            lp = Utilities.RevitUtils.SerializePoint(p1);
                                            lp2 = Utilities.RevitUtils.SerializePoint(p2);
                                        }
                                        else
                                        {
                                            continue; // not sure what this is???
                                        }
                                    }
                                }
                            }
                        }

                        Level lev = lookupLevel(e, p1);

                        pId.Value   = e.Id.AsLong();
                        pBbMn.Value = bbMin;
                        pBbMx.Value = bbMax;
                        pLoc.Value  = lp;
                        pLoc2.Value = lp2;
                        pLev.Value  = lev != null ? lev.Name : String.Empty;
                        pRot.Value  = rotation;

                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        private Level lookupLevel(Element e, XYZ pt)
        {
            // given the Element, figure out the level if possible.
            if (e.LevelId != ElementId.InvalidElementId) return e.Document.GetElement(e.LevelId) as Level;
            // otherwise, let's see if we can get it from the location point.

            if (pt == null) return null; // we don't know.

            if (_allLevels == null)
            {
                FilteredElementCollector coll = new FilteredElementCollector(_doc);
                coll.OfClass(typeof(Level));

                _allLevels = coll.Cast<Level>().ToList();
            }

            // we want the next level down from the z value...
            Level lev = Utilities.RevitUtils.GetNextLevelDown(pt, _allLevels);

            return lev;
        }
        
#endregion
    }
}
