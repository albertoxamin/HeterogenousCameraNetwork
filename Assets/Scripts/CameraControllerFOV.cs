﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using Geometry;
using Containers;

public class CameraControllerFOV : MonoBehaviour {

    private readonly float cameraWidth = 640f, cameraHeight = 480f, downscalingFactor = 20f, FOV = 100f, distanceDeviation = 2f;
    public float maxDistance = 100f;
    private float vFOV;
    //public Line positionedOn;

    Rect test;

    public List<GameObject> personHit = new List<GameObject>();
    public List<GameObject> personHitSizeChecked = new List<GameObject>();
    public List<Rect> boundingBoxes = new List<Rect>();
    public float margin = 0;


    public Camera cam;
    private Vector3[] pts = new Vector3[8];

    //camera fov and rendering
    private void FixCameraRes()//necessary to keep same camera resolution regardless of current screen ratio
    {
        float x, y, width, height, percent;
        if (Display.main.renderingWidth - cameraWidth < 0)
        {
            percent = Mathf.Abs((Display.main.renderingWidth - cameraWidth) / cameraWidth);
            width = cameraWidth - cameraWidth * percent;
            height = cameraHeight - cameraHeight * percent;
        }
        else { width = cameraWidth; }
        if (Display.main.renderingHeight - cameraHeight < 0)
        {
            percent = Mathf.Abs((Display.main.renderingHeight - cameraHeight) / cameraHeight);
            width = cameraWidth - cameraWidth * percent;
            height = cameraHeight - cameraHeight * percent;
        }
        else { height = cameraHeight; }
        x = (Display.main.renderingWidth - width) / 2f;
        y = (Display.main.renderingHeight - height) / 2f;
        GetComponent<Camera>().pixelRect = new Rect(x, y, width, height);
    }

    private void FixFOV()//necessary to set fov
    {
        float ratio = cameraWidth / cameraHeight;
        vFOV = 2f * Mathf.Atan(Mathf.Tan(FOV / 2f * Mathf.Deg2Rad) / ratio) * Mathf.Rad2Deg;
        GetComponent<Camera>().fieldOfView = vFOV;
    }

    public void UpdateGrids()
    {

    }

    public void InitialiseGrids()
    {

    }



    //raycasting and information on visual gathering
    public void FillVisibilityGrid() //fills the visibility grid with the qualities of view from this camera
    {
        //getting coordinates of all ground projections - Floor must have boxcollider
        Vector3[,,] projectedRays = ProjectRaysFromCamera();
        
        Bounds map = GameObject.Find("Floor").GetComponent<BoxCollider>().bounds;
        Bounds mapVolume = GameObject.Find("Map").GetComponent<MapController>().mapBounds;
        Vector3[,] onTheGroundProjections = new Vector3[projectedRays.GetLength(0), projectedRays.GetLength(1)];
        for (int i = 0; i < onTheGroundProjections.GetLength(0); i++)
        {
            for (int j = 0; j < onTheGroundProjections.GetLength(1); j++)
            {
                if ( mapVolume.Contains(projectedRays[i, j, 0]))
                {
                    //Debug.Log(projectedRays[i, j, 0]);
                    onTheGroundProjections[i, j] = map.ClosestPoint(projectedRays[i, j, 0]);
                }
                else
                {
                    onTheGroundProjections[i, j] = Vector3.zero;
                }
            }
        }

        Cell[,] grid = GameObject.Find("Map").GetComponent<GridController>().observationGrid;

        //declaring a fictious grid and initializing it, for quality of view of this camera
        float[,] proposedGrid = new float[grid.GetLength(0), grid.GetLength(1)];
        for (int i = 0; i < proposedGrid.GetLength(0); i++)
        {
            for (int j = 0; j < proposedGrid.GetLength(1); j++)
            {
                proposedGrid[i, j] = 0f;
            }
        }

        GridController pointer = GameObject.Find("Map").GetComponent<GridController>();

        //substituting each cell which has a higher QoV from the fictious grid to the actual grid
        for (int p1 = 0; p1 < onTheGroundProjections.GetLength(0); p1++)
        {
            for (int p2 = 0; p2 < onTheGroundProjections.GetLength(1); p2++)
            {
                for (int i = 0; i < proposedGrid.GetLength(0); i++)
                {
                    for (int j = 0; j < proposedGrid.GetLength(1); j++)
                    {
                        
                        if (onTheGroundProjections[p1, p2] != Vector3.zero & grid[i, j].Contains(onTheGroundProjections[p1, p2]))
                        {
                            //proposedGrid[i, j] += SpatialConfidence(projectedRays[p1, p2, 0]);
                            //update the timeConfidenceGridNewObs
                            pointer.UpdateTimeConfidenceGridNewObs(i,j);
                            pointer.UpdateSpatialConfidenceGridNewObs(i, j, SpatialConfidence(projectedRays[p1, p2, 0]));
                            //Debug.Log(onTheGroundProjections[p1, p2]);
                            //Debug.Log(projectedRays[i, j, 1]);
                            if (projectedRays[p1, p2, 1].magnitude > 1)
                            {
                                pointer.CountObservationGridNewObs(i, j);
                                //Debug.Log(i + "," + j);
                                //Debug.Log(projectedRays[i, j, 1].z);
                            }
                        }
                        if (p1 == (onTheGroundProjections.GetLength(0) - 1) && p2 == (onTheGroundProjections.GetLength(1) - 1)) //when I finished projecting every possible projection in this cell set the qualityofview of the grid to be the highest
                        {
                            //Debug.Log(onTheGroundProjections[p1, p2]);
                            if (grid[i, j].value < proposedGrid[i, j])
                            {
                                grid[i, j].value = proposedGrid[i, j];
                            }
                        }
                    }
                }
            }
        }
    }

    public Vector3[,,] ProjectRaysFromCamera() //returns a matrix with information about what rays coming out of the camera see
    {
        int layermask = 1 << 9; layermask = ~layermask; //layermask that lets me hit everything, needed because of how I call raycast
        float height, width, fractionaryHeight, fractionaryWidth;
        height = Mathf.Tan(vFOV / 2f * Mathf.Deg2Rad) * 2f;
        width = Mathf.Tan(FOV / 2f * Mathf.Deg2Rad) * 2f;
        fractionaryHeight = height / (cameraHeight / downscalingFactor);
        fractionaryWidth = width / (cameraWidth / downscalingFactor);
        Vector3 rectangleCenter = transform.position + transform.forward;
        Vector3[,,] res = new Vector3[(int)Mathf.Round(cameraHeight / downscalingFactor) + 1, (int)Mathf.Round(cameraWidth / downscalingFactor) + 1, 2]; //the +1 is needed to cover the whole FOV area without leaving the last fractionary portion out

        for (int i = 0; i < res.GetLength(0); i++)
        {
            for (int j = 0; j < res.GetLength(1); j++)
            {
                RaycastHit hit;
                Vector3 destinationPoint;
                if (i % 2 == 0 || j == res.GetLength(1) - 1 || j == 0)
                {
                    destinationPoint = (rectangleCenter - transform.right * width / 2f + transform.up * height / 2f) + i * fractionaryHeight * (-transform.up) + j * fractionaryWidth * transform.right;
                }
                else
                {
                    //spread the rays in alternating rows, so that I can effectively cover double the space horizontally
                    destinationPoint = (rectangleCenter - transform.right * width / 2f + transform.up * height / 2f + (fractionaryWidth / 2f) * transform.right) + i * fractionaryHeight * (-transform.up) + j * fractionaryWidth * transform.right;
                }
                Ray ray = new Ray(transform.position, destinationPoint - transform.position);

                if(Physics.Raycast(ray, out hit, maxDistance, layermask))
                {
                    res[i, j, 0] = hit.point;

                    //check if the person can be detected (check distance if it's close enough I'll detect it)
                    if (hit.transform.tag == "Person")
                    {
                        // check is face is big enough to be detected

                        // check if person already detected
                        //Debug.Log(res[i, j, 1]); 
                        //add 
                        
                        //hit.transform.GetComponent<Person>().PersonID;
                        //hit.transform.GetChild(1).transform;
                        //Debug.Log(hit.transform.name);
                        //Debug.Log(hit.transform.GetChild(2).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(1).name); //Neckadjust
                        //Debug.Log(hit.transform.GetChild(2).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0).GetChild(14).name); //Nosetop
                        //Debug.Log(this.GetComponent<Camera>().WorldToScreenPoint(hit.transform.GetChild(2).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(1).position));
                        //Debug.Log(this.GetComponent<Camera>().WorldToScreenPoint(hit.transform.GetChild(2).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0).GetChild(14).position));

                        

                        //test = GUIRectWithObject(hit.transform.gameObject);
                        //test = BoundsToScreenRect(hit.transform.gameObject.GetComponent<Collider>().bounds);
                        //Debug.Log(hit.transform.gameObject);
					

                        if (personHit.Contains(hit.transform.gameObject) != true)
                        {
                            personHit.Add(hit.transform.gameObject);
                            //Debug.Log(hit.transform.gameObject.name);
                            //res[i, j, 1] = new Vector3(1, 1, res[i, j, 1].z + 1);
                            Bounds b = hit.transform.GetChild(3).GetComponent<Renderer>().bounds;
                            //Debug.Log(b);
                            
                            //Debug.Log(cam);

                            //The object is behind us
                            if (cam.WorldToScreenPoint(b.center).z < 0) continue;

                            //All 8 vertices of the bounds
                            pts[0] = cam.WorldToScreenPoint(new Vector3(b.center.x + b.extents.x, b.center.y + b.extents.y, b.center.z + b.extents.z));
                            pts[1] = cam.WorldToScreenPoint(new Vector3(b.center.x + b.extents.x, b.center.y + b.extents.y, b.center.z - b.extents.z));
                            pts[2] = cam.WorldToScreenPoint(new Vector3(b.center.x + b.extents.x, b.center.y - b.extents.y, b.center.z + b.extents.z));
                            pts[3] = cam.WorldToScreenPoint(new Vector3(b.center.x + b.extents.x, b.center.y - b.extents.y, b.center.z - b.extents.z));
                            pts[4] = cam.WorldToScreenPoint(new Vector3(b.center.x - b.extents.x, b.center.y + b.extents.y, b.center.z + b.extents.z));
                            pts[5] = cam.WorldToScreenPoint(new Vector3(b.center.x - b.extents.x, b.center.y + b.extents.y, b.center.z - b.extents.z));
                            pts[6] = cam.WorldToScreenPoint(new Vector3(b.center.x - b.extents.x, b.center.y - b.extents.y, b.center.z + b.extents.z));
                            pts[7] = cam.WorldToScreenPoint(new Vector3(b.center.x - b.extents.x, b.center.y - b.extents.y, b.center.z - b.extents.z));

                            //Get them in GUI space
                            for (int ii = 0; ii < pts.Length; ii++) pts[ii].y = Screen.height - pts[ii].y;

                            //Calculate the min and max positions
                            Vector3 min = pts[0];
                            Vector3 max = pts[0];
                            for (int ii = 1; ii < pts.Length; ii++)
                            {
                                min = Vector3.Min(min, pts[ii]);
                                max = Vector3.Max(max, pts[ii]);
                            }

                            //Construct a rect of the min and max positions and apply some margin
                            test = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                            test.xMin -= margin;
                            test.xMax += margin;
                            test.yMin -= margin;
                            test.yMax += margin;

                            if (test.height > 25 && test.width > 25)
                            {
                                personHitSizeChecked.Add(hit.transform.gameObject);
                                boundingBoxes.Add(test);
                                res[i, j, 1] = new Vector3(1, 1, 1);

								if (GameObject.Find ("Map").GetComponent<GridController> ().people.Contains (hit.transform.gameObject) != true) 
								{
									GameObject.Find("Map").GetComponent<GridController>().people.Add(hit.transform.gameObject);
								}
                            }
                        }




                    }
                    //else
                    //{
                    //    res[i, j, 1] = new Vector3(0, 0, 0);
                    //}

                    Debug.DrawLine(transform.position, hit.point);
                }
                

            }
        }

        return res;
    }


    private void OnGUI()
    {
        ////GUI.Label(test, "test");
        ////GUI.DrawTexture(test, WhiteTexture);
        //foreach (var currHit in personHit)
        //{
        //    Bounds b = currHit.transform.GetChild(3).GetComponent<Renderer>().bounds;
        //    Camera cam = Camera.current;

        //    //The object is behind us
        //    //if (cam.WorldToScreenPoint(b.center).z < 0) continue;

        //    //All 8 vertices of the bounds
        //    pts[0] = cam.WorldToScreenPoint(new Vector3(b.center.x + b.extents.x, b.center.y + b.extents.y, b.center.z + b.extents.z));
        //    pts[1] = cam.WorldToScreenPoint(new Vector3(b.center.x + b.extents.x, b.center.y + b.extents.y, b.center.z - b.extents.z));
        //    pts[2] = cam.WorldToScreenPoint(new Vector3(b.center.x + b.extents.x, b.center.y - b.extents.y, b.center.z + b.extents.z));
        //    pts[3] = cam.WorldToScreenPoint(new Vector3(b.center.x + b.extents.x, b.center.y - b.extents.y, b.center.z - b.extents.z));
        //    pts[4] = cam.WorldToScreenPoint(new Vector3(b.center.x - b.extents.x, b.center.y + b.extents.y, b.center.z + b.extents.z));
        //    pts[5] = cam.WorldToScreenPoint(new Vector3(b.center.x - b.extents.x, b.center.y + b.extents.y, b.center.z - b.extents.z));
        //    pts[6] = cam.WorldToScreenPoint(new Vector3(b.center.x - b.extents.x, b.center.y - b.extents.y, b.center.z + b.extents.z));
        //    pts[7] = cam.WorldToScreenPoint(new Vector3(b.center.x - b.extents.x, b.center.y - b.extents.y, b.center.z - b.extents.z));

        //    //Get them in GUI space
        //    for (int i = 0; i < pts.Length; i++) pts[i].y = Screen.height - pts[i].y;

        //    //Calculate the min and max positions
        //    Vector3 min = pts[0];
        //    Vector3 max = pts[0];
        //    for (int i = 1; i < pts.Length; i++)
        //    {
        //        min = Vector3.Min(min, pts[i]);
        //        max = Vector3.Max(max, pts[i]);
        //    }

        //    //Construct a rect of the min and max positions and apply some margin
        //    test = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        //    test.xMin -= margin;
        //    test.xMax += margin;
        //    test.yMin -= margin;
        //    test.yMax += margin;

        //    //Render the box
        //    if (test.height > 25 && test.width > 25)
        //    {
        //        GUI.Box(test, "YES");
        //        personHitSizeChecked.Add(currHit);
        //    }
        //    else
        //    {
        //        GUI.Box(test, "NO");
        //        //personHit.Remove(currHit);
        //    }

        //}
        foreach(var box in boundingBoxes)
        {
            GUI.Box(box, "yes");
        }
    }


    private float SpatialConfidence(Vector3 coordinates) //tells the quality of view from the world coordinate to this camera
    {
        //float oneBySqrtTwoPi = 0.39894228f, distanceMinusOptimal = Vector3.Distance(transform.position, coordinates) - optimalDistance;
        //return oneBySqrtTwoPi / (distanceDeviation * distanceDeviation) * Mathf.Exp(-((distanceMinusOptimal * distanceMinusOptimal) / (2 * distanceDeviation * distanceDeviation)));

        //Debug.Log(Vector3.Distance(transform.position, coordinates));



        return 1 - (Vector3.Distance(transform.position, coordinates)/ maxDistance);
    }



    //Unity methods
    private void OnEnable()
    {
        FixFOV();
        transform.Rotate(20, 0, 0);
    }

    private void Update()
    {
        personHit.Clear();
        boundingBoxes.Clear();
        personHitSizeChecked.Clear();
        FixCameraRes();
        FillVisibilityGrid();
        //ProjectRaysFromCamera();
    }


}

