// Almost all code in this file from AMDs TressFx, MIT license - big thanks for sharing!
// https://github.com/GPUOpen-Effects/TressFX
// Notable additon in DistancePointToEdge() to fix numerical error artifacts.

// Copyright (c) 2017 Advanced Micro Devices, Inc. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

float3 g_Origin;
float g_CellSize;
int g_NumCellsX;
int g_NumCellsY;
int g_NumCellsZ;

#define MARGIN g_CellSize
#define GRID_MARGIN int3(1, 1, 1)

//Actually contains floats; make sure to use asfloat() when accessing. uint is used to allow atomics.
RWStructuredBuffer<uint> g_SignedDistanceField;

uint FloatFlip3(float fl)
{
	uint f = asuint(fl);
	return (f << 1) | (f >> 31);		//Rotate sign bit to least significant
}

uint IFloatFlip3(uint f2)
{
	return (f2 >> 1) | (f2 << 31);
}

int3 GetLocalCellPositionFromIndex(uint localCellIndex, int3 cellsPerDimensionLocal)
{
    uint cellsPerLine = (uint)cellsPerDimensionLocal.x;
    uint cellsPerPlane = (uint)(cellsPerDimensionLocal.x * cellsPerDimensionLocal.y);

    uint numPlanesZ = localCellIndex / cellsPerPlane;
    uint remainder = localCellIndex % cellsPerPlane;
    
    uint numLinesY = remainder / cellsPerLine;
    uint numCellsX = remainder % cellsPerLine;
    
    return int3((int)numCellsX, (int)numLinesY, (int)numPlanesZ);
}

float3 GetSdfCellPosition(int3 gridPosition)
{
    float3 cellCenter = float3(gridPosition.x, gridPosition.y, gridPosition.z);
	cellCenter += 0.5;
	cellCenter *= g_CellSize;
    cellCenter += g_Origin.xyz;
    
    return cellCenter;
}

// One thread for each cell. 
[numthreads(THREAD_GROUP_SIZE, 1, 1)]
void Initialize(uint GIndex : SV_GroupIndex, uint3 GId : SV_GroupID, uint3 DTid : SV_DispatchThreadID)
{
	int sdfCellIndex = GetVoxelIndex(GIndex, GId);
    if(sdfCellIndex >= _VoxelResolution.w)
        return;
    
    g_SignedDistanceField[sdfCellIndex] = FloatFlip3(INITIAL_DISTANCE);
}

// One thread per each cell. 
[numthreads(THREAD_GROUP_SIZE, 1, 1)]
void Finalize(uint GIndex : SV_GroupIndex, uint3 GId : SV_GroupID, uint3 DTid : SV_DispatchThreadID)
{
	int sdfCellIndex = GetVoxelIndex(GIndex, GId);
	if (sdfCellIndex >= _VoxelResolution.w)
        return;

	uint distance = g_SignedDistanceField[sdfCellIndex];
	g_SignedDistanceField[sdfCellIndex] = IFloatFlip3(distance);
}

// Get SDF cell index coordinates (x, y and z) from a point position in world space
int3 GetSdfCoordinates(float3 positionInWorld)
{
    float3 sdfPosition = (positionInWorld - g_Origin.xyz) / g_CellSize;
    
    int3 result;
    result.x = (int)sdfPosition.x;
    result.y = (int)sdfPosition.y;
    result.z = (int)sdfPosition.z;
    
    return result;
}

int GetSdfCellIndex(int3 gridPosition)
{
    int cellsPerLine = g_NumCellsX;
    int cellsPerPlane = g_NumCellsX * g_NumCellsY;

    return cellsPerPlane*gridPosition.z + cellsPerLine*gridPosition.y + gridPosition.x;
}

float DistancePointToEdge(float3 p, float3 x0, float3 x1, out float3 n)
{
//demo-team-begin
	// With finite numerical precision dist(P, AB) != dist(P, BA)
	// The above causes lots of issues with negative distance to edges winning over
	// positive distances to edges outside of the mesh, by being randomly ever so slightly smaller.
	// Fix by making sure they have a stable ordering (simplified to testing .x only) and then relying on the fact,
	// that the function encoding to uint gives preference to positive values.
	if (x0.x > x1.x)
	{
		float3 temp = x0;
		x0 = x1;
		x1 = temp;
	}
//demo-team-end

	float3 x10 = x1 - x0;

	float t = dot(x1 - p, x10) / dot(x10, x10);
	t = max(0.0f, min(t, 1.0f));

	float3 a = p - (t*x0 + (1.0f - t)*x1);
	float d = length(a);
	n = a / (d + 1e-30f);

	return d;
}

// Check if p is in the positive or negative side of triangle (x0, x1, x2)
// Positive side is where the normal vector of triangle ( (x1-x0) x (x2-x0) ) is pointing to.
float SignedDistancePointToTriangle(float3 p, float3 x0, float3 x1, float3 x2)
{
	float d = 0;
	float3 x02 = x0 - x2;
	float l0 = length(x02) + 1e-30f;
	x02 = x02 / l0;
	float3 x12 = x1 - x2;
	float l1 = dot(x12, x02);
	x12 = x12 - l1*x02;
	float l2 = length(x12) + 1e-30f;
	x12 = x12 / l2;
	float3 px2 = p - x2;

	float b = dot(x12, px2) / l2;
	float a = (dot(x02, px2) - l1*b) / l0;
	float c = 1 - a - b;

	// normal vector of triangle. Don't need to normalize this yet.
	float3 nTri = cross((x1 - x0), (x2 - x0));
	float3 n;

	float tol = 1e-8f;

	if (a >= -tol && b >= -tol && c >= -tol)
	{
		n = p - (a*x0 + b*x1 + c*x2);
		d = length(n);

		float3 n1 = n / d;
		float3 n2 = nTri / (length(nTri) + 1e-30f);		// if d == 0

		n = (d > 0) ? n1 : n2;
	}
	else
	{
		float3 n_12;
		float3 n_02;
		d = DistancePointToEdge(p, x0, x1, n);

		float d12 = DistancePointToEdge(p, x1, x2, n_12);
		float d02 = DistancePointToEdge(p, x0, x2, n_02);

		d = min(d, d12);
		d = min(d, d02);

		n = (d == d12) ? n_12 : n;
		n = (d == d02) ? n_02 : n;
	}

#ifdef SIGNED
	d = (dot(p - x0, nTri) < 0.f) ? -d : d;
#endif

	return d;
}

// One thread per each triangle
[numthreads(THREAD_GROUP_SIZE, 1, 1)]
void SplatTriangleDistances(uint GIndex : SV_GroupIndex, uint3 GId : SV_GroupID, uint3 DTid : SV_DispatchThreadID)
{
	uint triangleIndex = GId.x * THREAD_GROUP_SIZE + GIndex;

//demo-team-begin
	triangleIndex *= 3;
	float3 tri0 = GetPos(GetIndex(triangleIndex + 0));
	float3 tri1 = GetPos(GetIndex(triangleIndex + 1));
	float3 tri2 = GetPos(GetIndex(triangleIndex + 2));

	tri0 = mul(_WorldToLocal, float4(tri0, 1)).xyz;
	tri1 = mul(_WorldToLocal, float4(tri1, 1)).xyz;
	tri2 = mul(_WorldToLocal, float4(tri2, 1)).xyz;
//demo-team-end
		
	float3 aabbMin = min(tri0, min(tri1, tri2)) - float3(MARGIN, MARGIN, MARGIN);
	float3 aabbMax = max(tri0, max(tri1, tri2)) + float3(MARGIN, MARGIN, MARGIN);

	int3 gridMin = GetSdfCoordinates(aabbMin) - GRID_MARGIN;
	int3 gridMax = GetSdfCoordinates(aabbMax) + GRID_MARGIN;

	gridMin.x = max(0, min(gridMin.x, g_NumCellsX - 1));
	gridMin.y = max(0, min(gridMin.y, g_NumCellsY - 1));
	gridMin.z = max(0, min(gridMin.z, g_NumCellsZ - 1));

	gridMax.x = max(0, min(gridMax.x, g_NumCellsX - 1));
	gridMax.y = max(0, min(gridMax.y, g_NumCellsY - 1));
	gridMax.z = max(0, min(gridMax.z, g_NumCellsZ - 1));

	for (int z = gridMin.z; z <= gridMax.z; ++z)
		for (int y = gridMin.y; y <= gridMax.y; ++y)
			for (int x = gridMin.x; x <= gridMax.x; ++x)
			{
				int3 gridCellCoordinate = int3(x, y, z);
				int gridCellIndex = GetSdfCellIndex(gridCellCoordinate);
				float3 cellPosition = GetSdfCellPosition(gridCellCoordinate);

				float distance = SignedDistancePointToTriangle(cellPosition, tri0, tri1, tri2);
				//distance -= MARGIN;
				uint distanceAsUint = FloatFlip3(distance);
				InterlockedMin(g_SignedDistanceField[gridCellIndex], distanceAsUint);
			}
}