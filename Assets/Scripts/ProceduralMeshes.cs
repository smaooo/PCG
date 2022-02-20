using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

using static Unity.Mathematics.math;

namespace ProceduralMeshes
{
    
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public struct MeshJob<G, S> : IJobFor
        where G : struct, IMeshGenerator
        where S : struct, IMeshStreams
    {
        G generator;
        [WriteOnly]
        S streams;
        public void Execute(int i) => generator.Execute(i, streams);

        public static JobHandle ScheduleParallel (
            Mesh mesh, Mesh.MeshData meshData, int resolution, JobHandle dependency)
        {
            var job = new MeshJob<G, S>();
            job.generator.Resolution = resolution;
            job.streams.Setup(
                meshData,
                mesh.bounds = job.generator.Bounds,
                job.generator.VertexCount,
                job.generator.IndexCount);
            return job.ScheduleParallel(job.generator.JobLength, 1, dependency);
        }

    }
    public struct PVertex
    {
        public float3 position, normal;
        public float4 tangent;
        public float2 texCoord0;
    }

    public interface IMeshStreams
    {
        void Setup(Mesh.MeshData data, Bounds bounds, int vertexCount, int indexCount);

        void SetVertex(int index, PVertex data);

        void SetTriangle(int index, int3 triangle);

    }

    public interface IMeshGenerator
    {
        void Execute<S>(int i, S streams) where S : struct, IMeshStreams;

        int VertexCount { get; }

        int IndexCount { get; }

        int JobLength { get; }

        Bounds Bounds { get; }

        int Resolution { get; set; }
    }

}

namespace ProceduralMeshes.Streams
{
    public struct SingleStream : IMeshStreams
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TriangleUInt16
        {
            public ushort a, b, c;

            public static implicit operator TriangleUInt16(int3 t) => new TriangleUInt16
            {
                a = (ushort)t.x,
                b = (ushort)t.y,
                c = (ushort)t.z

            };
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Stream0
        {
            public float3 position, normal;
            public float4 tangent;
            public float2 texCoord0;
        }

        [NativeDisableContainerSafetyRestriction]
        NativeArray<Stream0> stream0;
        [NativeDisableContainerSafetyRestriction]
        NativeArray<TriangleUInt16> triangles;

        public void Setup(Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount)
        {
            NativeArray<VertexAttributeDescriptor> descriptors = new NativeArray<VertexAttributeDescriptor>(
                4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            descriptors[0] = new VertexAttributeDescriptor(dimension: 3);
            descriptors[1] = new VertexAttributeDescriptor(
                VertexAttribute.Normal, dimension: 3);
            descriptors[2] = new VertexAttributeDescriptor(
                VertexAttribute.Tangent, dimension: 4);
            descriptors[3] = new VertexAttributeDescriptor(
                VertexAttribute.TexCoord0, dimension: 2);
            meshData.SetVertexBufferParams(vertexCount, descriptors);
            descriptors.Dispose();

            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount)
            {
                bounds = bounds,
                vertexCount = vertexCount
            },
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);

            stream0 = meshData.GetVertexData<Stream0>();
            triangles = meshData.GetIndexData<ushort>().Reinterpret<TriangleUInt16>(2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int index, PVertex vertex) => stream0[index] = new Stream0
        {
            position = vertex.position,
            normal = vertex.normal,
            tangent = vertex.tangent,
            texCoord0 = vertex.texCoord0
        };

        public void SetTriangle(int index, int3 triangle) => triangles[index] = triangle;
    }

    public struct MultiStream : IMeshStreams
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TriangleUInt16
        {
            public ushort a, b, c;

            public static implicit operator TriangleUInt16(int3 t) => new TriangleUInt16
            {
                a = (ushort)t.x,
                b = (ushort)t.y,
                c = (ushort)t.z

            };
        }

        [NativeDisableContainerSafetyRestriction]
        NativeArray<float3> stream0, stream1;

        [NativeDisableContainerSafetyRestriction]
        NativeArray<float4> stream2;

        [NativeDisableContainerSafetyRestriction]
        NativeArray<float2> stream3;

        [NativeDisableContainerSafetyRestriction]
        NativeArray<TriangleUInt16> triangles;

        public void Setup(Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount)
        {
            NativeArray<VertexAttributeDescriptor> descriptors = new NativeArray<VertexAttributeDescriptor>(
                4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            descriptors[0] = new VertexAttributeDescriptor(dimension: 3);
            descriptors[1] = new VertexAttributeDescriptor(
                VertexAttribute.Normal, dimension: 3, stream: 1);
            descriptors[2] = new VertexAttributeDescriptor(
                VertexAttribute.Tangent, dimension: 4, stream: 2);
            descriptors[3] = new VertexAttributeDescriptor(
                VertexAttribute.TexCoord0, dimension: 2, stream: 3);
            meshData.SetVertexBufferParams(vertexCount, descriptors);
            descriptors.Dispose();

            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount)
            {
                bounds = bounds,
                vertexCount = vertexCount
            },
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);

            stream0 = meshData.GetVertexData<float3>();
            stream1 = meshData.GetVertexData<float3>(1);
            stream2 = meshData.GetVertexData<float4>(2);
            stream3 = meshData.GetVertexData<float2>(3);
            triangles = meshData.GetIndexData<ushort>().Reinterpret<TriangleUInt16>(2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int index, PVertex vertex)
        {
            stream0[index] = vertex.position;
            stream1[index] = vertex.normal;
            stream2[index] = vertex.tangent;
            stream3[index] = vertex.texCoord0;
        }


        public void SetTriangle(int index, int3 triangle) => triangles[index] = triangle;
    }
}



namespace ProceduralMeshes.Generators
{
    public struct SquareGrid : IMeshGenerator
    {
        public int VertexCount => 4 * Resolution * Resolution;

        public int IndexCount => 6 * Resolution * Resolution;

        public int JobLength => Resolution * Resolution;

        public int Resolution { get; set; }
        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(1f, 0f, 1f));

        public void Execute<S>(int i, S streams) where S : struct, IMeshStreams
        {
            int vi = 4 * i, ti = 2 * i;
            int z = i / Resolution;
            int x = i - Resolution * z;

            float4 coordinates = float4(x, x + 1f, z, z + 1f) / Resolution - 0.5f; 
            var vertex = new PVertex();
            vertex.normal.y = 1f;
            vertex.tangent.xw = float2(1f, -1f);

            vertex.position.xz = coordinates.xz;
            streams.SetVertex(vi + 0, vertex);

            vertex.position.xz = coordinates.yz;
            vertex.texCoord0 = float2(1f, 0f);
            streams.SetVertex(vi + 1, vertex);

            vertex.position.xz = coordinates.xw;
            vertex.texCoord0 = float2(0f, 1f);
            streams.SetVertex(vi + 2, vertex);

            vertex.position.xz = coordinates.yw;
            vertex.texCoord0 = 1f;
            streams.SetVertex(vi + 3, vertex);
            streams.SetTriangle(ti + 0, vi +int3(0, 2, 1));
            streams.SetTriangle(ti + 1, vi + int3(1, 2, 3));

        }
    }
}