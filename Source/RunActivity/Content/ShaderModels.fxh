// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// Shader model selection per graphics backend. mgfxc defines VULKAN or OPENGL itself;
// the build passes DIRECTX12 for the Direct3D 12 backend. Both Vulkan and D3D12 compile
// with DXC, which only accepts shader model 6.
//
// _LOW is the baseline every technique must run on; _HIGH is used by techniques that
// previously required feature level 9_3.

#if defined(VULKAN) || defined(DIRECTX12)
#define VS_LOW vs_6_0
#define PS_LOW ps_6_0
#define VS_HIGH vs_6_0
#define PS_HIGH ps_6_0
#elif defined(OPENGL)
#define SV_POSITION POSITION
#define SV_TARGET0 COLOR0
#define VS_LOW vs_3_0
#define PS_LOW ps_3_0
#define VS_HIGH vs_3_0
#define PS_HIGH ps_3_0
#else
#define VS_LOW vs_4_0_level_9_1
#define PS_LOW ps_4_0_level_9_1
#define VS_HIGH vs_4_0_level_9_3
#define PS_HIGH ps_4_0_level_9_3
#endif
