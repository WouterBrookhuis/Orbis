﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orbis.Rendering
{
    /// <summary>
    /// Stores data for a decoration type and allows access to it
    /// </summary>
    class DecorationData
    {
        private List<bool> occupation;
        private Mesh baseMesh;
        private List<RenderableMesh> combinedRenderableMesh;
        private List<bool> shouldBeUpdated;
        private GraphicsDevice device;
        int meshesPerCombi;

        /// <summary>
        /// Creates a new DecorationData for the given mesh.
        /// </summary>
        /// <param name="mesh">The decoration mesh</param>
        /// <param name="device">The graphics device to use</param>
        /// <param name="startMeshCount">The amount of combined meshes to start with</param>
        public DecorationData(Mesh mesh, GraphicsDevice device, int startMeshCount)
        {
            int maxInstances = ushort.MaxValue / mesh.VertexCount;
            var instances = new List<MeshInstance>();
            for (int i = 0; i < maxInstances; i++)
            {
                instances.Add(new MeshInstance
                {
                    mesh = mesh,
                    matrix = Matrix.Identity,
                    tag = i,
                });
            }
            this.device = device;
            baseMesh = mesh;
            combinedRenderableMesh = new List<RenderableMesh>();
            occupation = new List<bool>();
            shouldBeUpdated = new List<bool>();
            meshesPerCombi = maxInstances;
            for (int i = 0; i < startMeshCount * maxInstances; i++)
            {
                occupation.Add(false);
            }
            for (int i = 0; i < startMeshCount; i++)
            {
                shouldBeUpdated.Add(true);
                combinedRenderableMesh.Add(new RenderableMesh(device, new Mesh(instances)));
            }
        }

        private void AddCombiMesh()
        {
            int maxInstances = ushort.MaxValue / baseMesh.VertexCount;
            var instances = new List<MeshInstance>();
            for (int i = 0; i < maxInstances; i++)
            {
                instances.Add(new MeshInstance
                {
                    mesh = baseMesh,
                    matrix = Matrix.Identity,
                    tag = i,
                });
            }
            shouldBeUpdated.Add(true);
            combinedRenderableMesh.Add(new RenderableMesh(device, new Mesh(instances)));
            for(int i = 0; i < maxInstances; i++)
            {
                occupation.Add(false);
            }
        }

        public int GetFreeIndex()
        {
            for (int i = 0; i < occupation.Count; i++)
            {
                if (occupation[i] == false)
                {
                    occupation[i] = true;
                    return i;
                }
            }
            // Add another combi mesh
            AddCombiMesh();
            return GetFreeIndex();
        }

        public void FreeIndex(int index)
        {
            if (index >= 0 && index < occupation.Count)
            {
                occupation[index] = false;
                SetPosition(index, Vector3.Zero);
            }
        }

        public void SetPosition(int index, Vector3 position)
        {
            if (index < 0 || index >= occupation.Count)
            {
                throw new IndexOutOfRangeException();
            }

            int meshIndex = index / this.meshesPerCombi;
            int offset = (index - meshIndex * this.meshesPerCombi) * baseMesh.VertexCount;
            for (int i = 0; i < baseMesh.VertexCount; i++)
            {
                combinedRenderableMesh[meshIndex].VertexData[i + offset].Position = baseMesh.Vertices[i] + position;
            }
            shouldBeUpdated[meshIndex] = true;
        }

        public void Update(HashSet<RenderableMesh> updateSet)
        {
            for (int i = 0; i < shouldBeUpdated.Count; i++)
            {
                if (shouldBeUpdated[i])
                {
                    updateSet.Add(combinedRenderableMesh[i]);
                    shouldBeUpdated[i] = false;
                }
            }
        }

        public List<RenderInstance> GetActiveInstances()
        {
            var instances = new List<RenderInstance>();
            for (int i = 0; i < this.combinedRenderableMesh.Count; i++)
            {
                bool render = false;
                for (int k = 0; k < this.meshesPerCombi; k++)
                {
                    if (this.occupation[i * this.meshesPerCombi + k] == true)
                    {
                        render = true;
                        break;
                    }
                }
                if (render)
                {
                    instances.Add(new RenderInstance()
                    {
                        mesh = combinedRenderableMesh[i],
                        matrix = Matrix.Identity,
                    });
                }
            }
            return instances;
        }
    }
}