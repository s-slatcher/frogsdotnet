using Godot;
using System;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection.Metadata;


public partial class DistortableCompositePolygonMeshUpdater : Node3D
{

    PolygonQuadMesh quadMesh;

    QuadMeshDistortionApplier distortionApplier;
    Dictionary<Rect2, MeshInstance3D> MeshInstanceMap = new();

    List<IQuadMeshDistorter> distortionQueue = new();
    DistortionTask activeDistort = new();

    HashSet<Rect2> meshRegionUpdateQueue = new(); // HashSet over list to avoid duplicates & because entire queue processed at once
    List<MeshTask> activeMeshUpdates = new();

    

    private struct DistortionTask
    {
        public Task Task;
        public IQuadMeshDistorter Distorter;
    }

    private struct MeshTask
    {
        public Task<Mesh> Task;
        public Rect2 Region;
    }

    
    public void SetQuadMesh(PolygonQuadMesh _quadMesh)
    {

        quadMesh = _quadMesh;
        distortionApplier = new(quadMesh);
        var meshSize = quadMesh.MeshSize;



        var baseDistort = new BaseTerrainDistorter(meshSize);
        distortionApplier.AddMeshDistorter(baseDistort);

        // populate mesh instance map, keying meshes to their regions
        foreach (Rect2 region in baseDistort.LeafNodeRegions)
        {
            var meshInst = new MeshInstance3D();
            MeshInstanceMap[region] = meshInst;
            AddChild(meshInst);
        }

        QueueMeshUpdates(baseDistort);

    }


    public override void _PhysicsProcess(double delta)
    {
        ProcessDistortionQueue();
        ProcessMeshQueue();
    }

    public List<MeshInstance3D> GetMeshInstances()
    {
        return MeshInstanceMap.Values.ToList();
    }

    public void DistortAndUpdate(IQuadMeshDistorter distorter)
    {
        distortionQueue.Add(distorter);
    }
    private void ProcessDistortionQueue()
    {
        var activeTask = activeDistort.Task;

        if (activeTask != null && activeTask.IsCompleted)
        {
            QueueMeshUpdates(activeDistort.Distorter);
            activeDistort = new DistortionTask(); // sets 'task' property null
        }
        else if (activeTask == null && distortionQueue.Count != 0)  // task IS null and distortion queue is not empty
        {
            var nextDistorter = distortionQueue[0];
            distortionQueue.RemoveAt(0);
            var distortDelegate = new Action(() => distortionApplier.AddMeshDistorter(nextDistorter));

            activeDistort = new DistortionTask() { Distorter = nextDistorter, Task = Task.Run(distortDelegate) };
        }

    }

    private void ProcessMeshQueue()
    {

        // search for finished mesh tasks -- update mesh and remove from list

        if (activeMeshUpdates.Count != 0)
        {
            var stillActiveList = new List<MeshTask>();


            foreach (MeshTask meshTask in activeMeshUpdates)
            {
                if (meshTask.Task.IsCompleted)
                {
                    UpdateMesh(meshTask.Task.Result, meshTask.Region);
                }
                else stillActiveList.Add(meshTask);
            }
            activeMeshUpdates = new(stillActiveList);

        }

        // start new mesh update tasks

        if (distortionQueue.Count != 0 || activeDistort.Task != null) return; // only process meshes once all distortions applied

        var recentQuadMesh = distortionApplier.GetQuadMesh();

        foreach (Rect2 region in meshRegionUpdateQueue)
        {
            var instance = MeshInstanceMap[region];
            var meshDelegate = Task.Run(() => recentQuadMesh.GenerateMesh(region));
            var meshTask = new MeshTask() { Region = region, Task = meshDelegate };
            activeMeshUpdates.Add(meshTask);
        }

        meshRegionUpdateQueue = new();
    }

    private void UpdateMesh(Mesh newMesh, Rect2 region)
    {
        MeshInstanceMap[region].Mesh = newMesh;
    }


   

    private void QueueMeshUpdates(IQuadMeshDistorter distorter)
    {
        var affectedRegions = MeshInstanceMap
            .Keys
            .Where(rect => distortionApplier.DistortersActiveOnQuad(rect).Contains(distorter))
            .ToList();


        meshRegionUpdateQueue.UnionWith(affectedRegions);
    }

    
}



