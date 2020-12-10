using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using OpenProject.Revit.Data;
using OpenProject.Revit.Extensions;
using OpenProject.Shared;
using OpenProject.Shared.ViewModels.Bcf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenProject.Revit.Entry
{
  /// <summary>
  /// Obfuscation Ignore for External Interface
  /// </summary>
  public class OpenViewpointEventHandler : IExternalEventHandler
  {
    /// <summary>
    /// This is the method declared in the <see cref="IExternalEventHandler"/> interface
    /// provided by the Revit API
    /// </summary>
    /// <param name="app"></param>
    public void Execute(UIApplication app)
    {
      ShowBcfViewpointInternal(app);
    }

    public string GetName() => nameof(OpenViewpointEventHandler);

    private static int _viewSequence = 0;
    private BcfViewpointViewModel _bcfViewpoint;

    private static OpenViewpointEventHandler _instance;
    private static OpenViewpointEventHandler Instance {
      get
      {
        if (_instance == null)
        {
          _instance = new OpenViewpointEventHandler();
          ExternalEvent = ExternalEvent.Create(_instance);
        }

        return _instance;
      }
    }
    private static ExternalEvent ExternalEvent { get; set; }

    /// <summary>
    /// External Event Implementation
    /// </summary>
    /// <param name="app"></param>
    public static void ShowBcfViewpoint(UIApplication app, BcfViewpointViewModel bcfViewpoint)
    {
      Instance._bcfViewpoint = bcfViewpoint;
      ExternalEvent.Raise();
    }

    private void ShowBcfViewpointInternal(UIApplication app)
    {
      try
      {
        UIDocument uidoc = app.ActiveUIDocument;
        Document doc = uidoc.Document;

        // IS ORTHOGONAL
        if (_bcfViewpoint.OrthogonalCamera != null)
        {
          ShowOrthogonalView(_bcfViewpoint, doc, uidoc, app);
        }
        //perspective
        else if (_bcfViewpoint.PerspectiveCamera != null)
        {
          ShowPerspectiveView(_bcfViewpoint, doc, uidoc);
        }
        else
        {
          //no view included
          return;
        }

        ApplyElementStyles(_bcfViewpoint, doc, uidoc);

        // The local callback first needs to be initialized to null since we're
        // referencing itself in it's body.
        // The reason for this is that we need to wait for Revit to load the view
        // and prepare everything. After that, we're waiting for the 'Idle' event
        // and instruct Revit to refresh and redraw the view. Otherwise, component
        // selection seemed not to work properly.
        EventHandler<IdlingEventArgs> afterIdleEventHandler = null;
        afterIdleEventHandler = (_, _) =>
        {
            uidoc.RefreshActiveView();
            app.ActiveUIDocument.UpdateAllOpenViews();
            app.Idling -= afterIdleEventHandler;
        };

        app.Idling += afterIdleEventHandler;
      }
      catch (Exception ex)
      {
        TaskDialog.Show("Error!", "exception: " + ex);
      }
    }

    private void ShowOrthogonalView(BcfViewpointViewModel bcfViewpoint, Document doc, UIDocument uidoc, UIApplication app)
    {
      if (bcfViewpoint.OrthogonalCamera == null)
        return;
      //type = "OrthogonalCamera";
      var zoom = bcfViewpoint.OrthogonalCamera.ViewToWorldScale.ToInternalRevitUnit();
      var cameraDirection = RevitUtils.GetRevitXYZ(bcfViewpoint.OrthogonalCamera.DirectionX,
        bcfViewpoint.OrthogonalCamera.DirectionY,
        bcfViewpoint.OrthogonalCamera.DirectionZ);
      var cameraUpVector = RevitUtils.GetRevitXYZ(bcfViewpoint.OrthogonalCamera.UpX,
        bcfViewpoint.OrthogonalCamera.UpY,
        bcfViewpoint.OrthogonalCamera.UpZ);
      var cameraViewPoint = RevitUtils.GetRevitXYZ(bcfViewpoint.OrthogonalCamera.ViewPointX,
        bcfViewpoint.OrthogonalCamera.ViewPointY,
        bcfViewpoint.OrthogonalCamera.ViewPointZ);
      var orient3D = RevitUtils.ConvertBasePoint(doc, cameraViewPoint, cameraDirection, cameraUpVector, true);

      View3D orthoView = null;
      //if active view is 3d ortho use it
      if (doc.ActiveView.ViewType == ViewType.ThreeD)
      {
        var activeView3D = doc.ActiveView as View3D;
        if (!activeView3D.IsPerspective)
          orthoView = activeView3D;
      }
      if (orthoView == null)
      {
        //try to use an existing 3D view
        IEnumerable<View3D> viewcollector3D = get3DViews(doc);
        if (viewcollector3D.Any(o => o.Name == "{3D}" || o.Name == "BCFortho"))
          orthoView = viewcollector3D.First(o => o.Name == "{3D}" || o.Name == "BCFortho");
      }
      using (var trans = new Transaction(uidoc.Document))
      {
        if (trans.Start("Open orthogonal view") == TransactionStatus.Started)
        {
          if (orthoView == null)
          {
            orthoView = View3D.CreateIsometric(doc, getFamilyViews(doc).First().Id);
            orthoView.Name = "BCFortho";
          }
          else
          {
            orthoView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
          }
          orthoView.SetOrientation(orient3D);
          trans.Commit();
        }
      }
      uidoc.ActiveView = orthoView;

      var viewChangedZoomListener = new ActiveViewChangedZoomListener(orthoView.Id.IntegerValue, zoom, app);
      viewChangedZoomListener.ListenForActiveViewChangeAndSetZoom();
    }

    private static void ShowPerspectiveView(BcfViewpointViewModel bcfViewpoint, Document doc, UIDocument uidoc)
    {
      if (bcfViewpoint.PerspectiveCamera == null)
        return;

      bcfViewpoint.EnsurePerspectiveCameraVectorsAreOrthogonal();

      //not used since the fov cannot be changed in Revit
      var zoom = bcfViewpoint.PerspectiveCamera.FieldOfView;
      //FOV - not used
      //double z1 = 18 / Math.Tan(zoom / 2 * Math.PI / 180);
      //double z = 18 / Math.Tan(25 / 2 * Math.PI / 180);
      //double factor = z1 - z;

      var cameraDirection = RevitUtils.GetRevitXYZ(bcfViewpoint.PerspectiveCamera.DirectionX,
        bcfViewpoint.PerspectiveCamera.DirectionY,
        bcfViewpoint.PerspectiveCamera.DirectionZ);
      var cameraUpVector = RevitUtils.GetRevitXYZ(bcfViewpoint.PerspectiveCamera.UpX,
        bcfViewpoint.PerspectiveCamera.UpY,
        bcfViewpoint.PerspectiveCamera.UpZ);
      var cameraViewPoint = RevitUtils.GetRevitXYZ(bcfViewpoint.PerspectiveCamera.ViewPointX,
        bcfViewpoint.PerspectiveCamera.ViewPointY,
        bcfViewpoint.PerspectiveCamera.ViewPointZ);
      var orient3D = RevitUtils.ConvertBasePoint(doc, cameraViewPoint, cameraDirection, cameraUpVector, false);

      View3D perspView = null;
      //try to use an existing 3D view
      IEnumerable<View3D> viewcollector3D = get3DViews(doc);
      if (viewcollector3D.Any(o => o.Name == "BCFpersp"))
        perspView = viewcollector3D.First(o => o.Name == "BCFpersp");

      using (var trans = new Transaction(uidoc.Document))
      {
        if (trans.Start("Open perspective view") == TransactionStatus.Started)
        {
          if (null == perspView)
          {
            perspView = View3D.CreatePerspective(doc, getFamilyViews(doc).First().Id);
            perspView.Name = "BCFpersp";
          }
          else
          {
            //reusing an existing view, I net to reset the visibility
            //placed this here because if set afterwards it doesn't work
            perspView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
          }

          perspView.SetOrientation(orient3D);

          // turn off the far clip plane
          if (perspView.get_Parameter(BuiltInParameter.VIEWER_BOUND_ACTIVE_FAR).HasValue)
          {
            Parameter m_farClip = perspView.get_Parameter(BuiltInParameter.VIEWER_BOUND_ACTIVE_FAR);
            m_farClip.Set(0);
          }
          perspView.CropBoxActive = false;
          perspView.CropBoxVisible = false;

          trans.Commit();
        }
      }

      uidoc.RequestViewChange(perspView);
    }

    private static void ApplyElementStyles(BcfViewpointViewModel bcfViewpoint, Document doc, UIDocument uidoc)
    {
      if (bcfViewpoint.Components == null)
        return;

      var elementsToSelect = new List<ElementId>();
      var elementsToHide = new List<ElementId>();
      var elementsToShow = new List<ElementId>();

      var visibleElems = new FilteredElementCollector(doc, doc.ActiveView.Id)
      .WhereElementIsNotElementType()
      .WhereElementIsViewIndependent()
      .ToElementIds()
      .Where(e => doc.GetElement(e).CanBeHidden(doc.ActiveView)); //might affect performance, but it's necessary

      //loop elements
      foreach (var e in visibleElems)
      {
        var guid = IfcGuid.ToIfcGuid(ExportUtils.GetExportId(doc, e));

        if (bcfViewpoint.Components.Any(c => !c.IsVisible && c.IfcGuid == guid))
        {
          elementsToHide.Add(e);
        }

        if (bcfViewpoint.Components.Any(c => c.IsVisible && c.IfcGuid == guid))
        {
          elementsToShow.Add(e);
        }

        if (bcfViewpoint.Components.Any(c => c.IsSelected && c.IfcGuid == guid))
        {
          // Selection always means showing a component, too
          elementsToShow.Add(e);
          elementsToSelect.Add(e);
        }
      }

      using (var trans = new Transaction(uidoc.Document))
      {
        if (trans.Start("Apply BCF visibility and selection") == TransactionStatus.Started)
        {
          if (elementsToHide.Any())
          {
            doc.ActiveView.HideElementsTemporary(elementsToHide);
          }
          else if (elementsToShow.Any())
          {
            // TODO: After support for default visibility is added and if the default visibility is false,
            // we should use 'doc.ActiveView.IsolateElementsTemporary(elementsToShow);' to only show
            // visible elements and hide all else
            doc.ActiveView.UnhideElements(elementsToShow);
          }

          if (elementsToSelect.Any())
          {
            uidoc.Selection.SetElementIds(elementsToSelect);
          }
        }
        trans.Commit();
      }
    }

    private static IEnumerable<ViewFamilyType> getFamilyViews(Document doc)
    {
      return from elem in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
             let type = elem as ViewFamilyType
             where type.ViewFamily == ViewFamily.ThreeDimensional
             select type;
    }

    private static IEnumerable<View3D> get3DViews(Document doc)
    {
      return from elem in new FilteredElementCollector(doc).OfClass(typeof(View3D))
             let view = elem as View3D
             select view;
    }

    private static IEnumerable<View> getSheets(Document doc, int id, string stname)
    {
      ElementId eid = new ElementId(id);
      return from elem in new FilteredElementCollector(doc).OfClass(typeof(View))
             let view = elem as View
             //Get the view with the given Id or given name
             where view.Id == eid | view.Name == stname
             select view;
    }
  }
}