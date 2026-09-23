#!/usr/bin/env python3
# COPYRIGHT 2026 by the Riel project.
#
# This file is part of Riel, a fork of Open Rails.
#
# Riel is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# Riel is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with Riel.  If not, see <http://www.gnu.org/licenses/>.

"""Writes a small MSTS installation the simulator can load and drive, from nothing.

Microsoft Train Simulator content cannot be redistributed, and without content the simulator
cannot get past its loading screen - which is where most of what can go wrong on a new platform
starts. This builds just enough to go all the way: a 1.5 km straight track with its track
sections, track database and signal configuration, two paths along it, environment and sound
files, and a diesel with a textured box for a body, exhaust effects and a 2D cab. Two activities
and three consists give the launcher something to list.

    python3 make_test_route.py <folder>

writes "<folder>/Train Simulator". Add it with `riel content add Test "<folder>/Train Simulator"`
and start it with `riel explore "Riel Test" "Mine to Port" Coal`.

To check for OpenGL calls made without a context - harmless on Mesa, a crash with NVIDIA's
driver - run the simulator with libglvnd's application error checking:

    __GLVND_APP_ERROR_CHECKING=1 __GLVND_ABORT_ON_APP_ERROR=1 riel explore ...
"""

import pathlib
import struct
import sys

root = pathlib.Path(sys.argv[1]) / "Train Simulator"
route = root / "ROUTES" / "RIELTEST"
TX, TZ = -6, 12                           # the tile everything is on (RouteStart)
SECTIONS = 15                             # 100 m straights
Z0 = -750.0                               # track runs along +Z from here


def box_shape(width, height, length, image, base=0.0):
    """A closed box, width across (x), height up (y), length along (z), sitting on y=base."""
    w, h, l = width / 2, height, length / 2
    faces = [  # normal, four corners (counter-clockwise seen from outside)
        ((0, 0, -1), [(-w, base, -l), (w, base, -l), (w, base + h, -l), (-w, base + h, -l)]),
        ((0, 0, 1), [(w, base, l), (-w, base, l), (-w, base + h, l), (w, base + h, l)]),
        ((-1, 0, 0), [(-w, base, l), (-w, base, -l), (-w, base + h, -l), (-w, base + h, l)]),
        ((1, 0, 0), [(w, base, -l), (w, base, l), (w, base + h, l), (w, base + h, -l)]),
        ((0, 1, 0), [(-w, base + h, -l), (w, base + h, -l), (w, base + h, l), (-w, base + h, l)]),
        ((0, -1, 0), [(-w, base, l), (w, base, l), (w, base, -l), (-w, base, -l)]),
    ]
    points, normals, vertices, tris, tri_normals = [], [], [], [], []
    uv = [(0, 1), (1, 1), (1, 0), (0, 0)]
    for n_index, (normal, corners) in enumerate(faces):
        normals.append(normal)
        start = len(vertices)
        for c_index, corner in enumerate(corners):
            points.append(corner)
            vertices.append((len(points) - 1, n_index, c_index))
        tris += [(start, start + 2, start + 1), (start, start + 3, start + 2)]
        tri_normals += [n_index, n_index]
    f = lambda v: " ".join(f"{x:g}" for x in v)
    out = ["SIMISA@@@@@@@@@@JINX0s1t______", "", "shape ("]
    out.append("\tshape_header ( 00000000 00000000 )")
    out.append(f"\tvolumes ( 1 vol_sphere ( vector ( 0 {base + h / 2:g} 0 ) {max(w, h, l) * 1.5:g} ) )")
    out.append("\tshader_names ( 1 named_shader ( TexDiff ) )")
    out.append("\ttexture_filter_names ( 1 named_filter_mode ( MipLinear ) )")
    out.append(f"\tpoints ( {len(points)} " + " ".join(f"point ( {f(p)} )" for p in points) + " )")
    out.append(f"\tuv_points ( {len(uv)} " + " ".join(f"uv_point ( {f(p)} )" for p in uv) + " )")
    out.append(f"\tnormals ( {len(normals)} " + " ".join(f"vector ( {f(n)} )" for n in normals) + " )")
    out.append("\tsort_vectors ( 0 )")
    out.append("\tcolours ( 0 )")
    out.append("\tmatrices ( 1 matrix MAIN ( 1 0 0 0 1 0 0 0 1 0 0 0 ) )")
    out.append(f"\timages ( 1 image ( {image} ) )")
    out.append("\ttextures ( 1 texture ( 0 0 0 ff000000 ) )")
    out.append("\tlight_materials ( 0 )")
    out.append("\tlight_model_cfgs ( 1 light_model_cfg ( 00000000 uv_ops ( 1 uv_op_copy ( 1 0 ) ) ) )")
    out.append("\tvtx_states ( 1 vtx_state ( 00000000 0 -5 0 00000002 ) )")
    out.append("\tprim_states ( 1 prim_state ( 00000000 0 tex_idxs ( 1 0 ) 0 0 0 0 1 ) )")
    verts = " ".join(f"vertex ( 00000000 {p} {n} ffffffff ff000000 vertex_uvs ( 1 {u} ) )" for p, n, u in vertices)
    idxs = " ".join(f"{a} {b} {c}" for a, b, c in tris)
    nidx = " ".join(f"{n} 3" for n in tri_normals)
    flags = " ".join("00000000" for _ in tris)
    sub = (f"sub_object ( sub_object_header ( 00000400 -1 -1 000001d2 000001c4 "
           f"geometry_info ( {len(tris)} 1 0 {len(tris) * 3} 0 0 1 0 0 0 "
           f"geometry_nodes ( 1 geometry_node ( 1 0 1 0 0 cullable_prims ( 1 {len(tris)} {len(tris) * 3} ) ) ) "
           f"geometry_node_map ( 1 0 ) ) subobject_shaders ( 1 0 ) subobject_light_cfgs ( 1 0 ) 0 ) "
           f"vertices ( {len(vertices)} {verts} ) vertex_sets ( 1 vertex_set ( 0 0 {len(vertices)} ) ) "
           f"primitives ( 2 prim_state_idx ( 0 ) indexed_trilist ( vertex_idxs ( {len(tris) * 3} {idxs} ) "
           f"normal_idxs ( {len(tris)} {nidx} ) flags ( {len(tris)} {flags} ) ) ) )")
    out.append("\tlod_controls ( 1 lod_control ( distance_levels_header ( 0 ) distance_levels ( 1 "
               "distance_level ( distance_level_header ( dlevel_selection ( 2000 ) hierarchy ( 1 -1 ) ) "
               f"sub_objects ( 1 {sub} ) ) ) ) )")
    out.append(")")
    return "\n".join(out) + "\n"

def ace(size, rgb, height=None, mipmaps=True):
    """An uncompressed ACE of one colour with a darker 2-pixel border: size x size and mipmapped,
    or size x height with a single level, as 2D cab views are."""
    width, height = size, height or size
    levels = []
    s = size
    while s >= 1:
        levels.append(s)
        s //= 2
        if not mipmaps:
            break
    data = bytearray(b"SIMISA@@@@@@@@@@")
    data += struct.pack("<iiiiii", 1, 0x01 if mipmaps else 0, width, height, 0x0E, 3)
    data += bytes(128)
    for channel in (3, 4, 5):                      # red, green, blue, 8 bits each
        data += struct.pack("<QQ", 8, channel)
    for s in levels:                               # scanline offset tables (ignored by the reader)
        data += bytes(4 * (s * height // width))
    for s in levels:
        w, h = s, s * height // width
        for y in range(h):
            for c in range(3):
                row = bytearray()
                for x in range(w):
                    edge = x < 2 or y < 2 or x >= w - 2 or y >= h - 2
                    row.append(rgb[c] // 2 if edge and w >= 8 else rgb[c])
                data += row
    return bytes(data)


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text.replace("\n", "\r\n"), encoding="utf-8")

# --- GLOBAL/tsection.dat: one 100 m straight and the shape that holds it
write(root / "GLOBAL" / "TSECTION.DAT", """SIMISA@@@@@@@@@@JINX0T0t______

TrackSections ( 1
	TrackSection ( 1
		SectionSize ( 1.5 100 )
	)
)
TrackShapes ( 1
	TrackShape ( 1
		FileName ( A1t100mStrt.s )
		NumPaths ( 1 )
		SectionIdx ( 1 0 0 0 0 1 )
	)
)
""")

# --- the track database: end node, one vector node of straights, end node
vs = []
for i in range(SECTIONS):
    z = Z0 + i * 100
    vs.append(f"1 1 {TX} {TZ} {i + 1} 0 1 00 {TX} {TZ} 0 0 {z:.1f} 0 0 0")
z_end = Z0 + SECTIONS * 100
write(route / "RIELTEST.TDB", f"""SIMISA@@@@@@@@@@JINX0T0t______

TrackDB (
	Serial ( 1 )
	TrackNodes ( 3
		TrackNode ( 1
			TrEndNode ( 0 )
			UiD ( {TX} {TZ} 100 0 {TX} {TZ} 0 0 {Z0:.1f} 0 0 0 )
			TrPins ( 1 0
				TrPin ( 2 1 )
			)
		)
		TrackNode ( 2
			TrVectorNode (
				TrVectorSections ( {SECTIONS} {' '.join(vs)} )
			)
			TrPins ( 1 1
				TrPin ( 1 1 )
				TrPin ( 3 1 )
			)
		)
		TrackNode ( 3
			TrEndNode ( 0 )
			UiD ( {TX} {TZ} 101 0 {TX} {TZ} 0 0 {z_end:.1f} 0 0 0 )
			TrPins ( 1 0
				TrPin ( 2 0 )
			)
		)
	)
)
""")

# --- paths along it: start near one end, finish near the other
def path(name, title, start, end, z_from, z_to):
    return f"""SIMISA@@@@@@@@@@JINX0P0t______

Serial ( 1 )
TrackPDPs (
	TrackPDP ( {TX} {TZ} 0 0 {z_from:.1f} 2 0 )
	TrackPDP ( {TX} {TZ} 0 0 {z_to:.1f} 2 0 )
)
TrackPath (
	TrPathName ( {name} )
	Name ( "{title}" )
	TrPathStart ( {start} )
	TrPathEnd ( {end} )
	TrPathNodes ( 2
		TrPathNode ( 00000000 1 4294967295 0 )
		TrPathNode ( 00000000 4294967295 4294967295 1 )
	)
)
"""
write(route / "PATHS" / "MAIN.PAT", path("MAIN", "Mine to Port", "Mine", "Port", Z0 + 150, Z0 + SECTIONS * 100 - 150))
write(route / "PATHS" / "BRANCH.PAT", path("BRANCH", "Port to Upper Yard", "Port", "Upper", Z0 + SECTIONS * 100 - 150, Z0 + 150))

# --- a diesel with exhaust, so the particle emitters run
write(root / "TRAINS" / "TRAINSET" / "BNSF" / "TESTLOCO.ENG", """SIMISA@@@@@@@@@@JINX0D0t______

Wagon ( TESTLOCO
	Type ( Engine )
	WagonShape ( testloco.s )
	Size ( 3m 4.5m 20m )
	Mass ( 100t )
	WheelRadius ( 0.5m )
	NumWheels ( 12 )
	Friction ( 7.5N/m/s 0 0 0 0 7.5N/m/s 0 0 0 0 )
	MaxBrakeForce ( 200kN )
	BrakeSystemType ( Air_single_pipe )
	Coupling (
		Type ( Automatic )
		Spring ( Break ( 1e7N 1e7N ) R0 ( 0cm 0cm ) Stiffness ( 1e6N/m 5e6N/m ) )
	)
	Name ( "Test Locomotive" )
)
Engine ( TESTLOCO
	Name ( "Test Locomotive" )
	Type ( Diesel )
	CabView ( testloco.cvf )
	MaxPower ( 2000kW )
	MaxForce ( 300kN )
	MaxVelocity ( 100kph )
	DieselEngineIdleRPM ( 300 )
	DieselEngineMaxRPM ( 900 )
	MaxDieselLevel ( 5000L )
	DieselUsedPerHourAtMaxPower ( 400L )
	DieselUsedPerHourAtIdle ( 10L )
	EngineControllers (
		Throttle ( 0 1 0.125 0 )
		Brake_Train ( 0 1 0.1 0
			NumNotches ( 2
				Notch ( 0 0 TrainBrakesControllerReleaseStart )
				Notch ( 1 0 TrainBrakesControllerFullServiceStart )
			)
		)
		DirControl ( -1 0 1 1 )
	)
	Effects (
		DieselSpecialEffects (
			Exhaust1 ( 0 4.5 2 0 1 0 0.2 )
			Exhaust2 ( 0 4.5 4 0 1 0 0.2 )
		)
	)
)
""")

# --- a textured box for the locomotive
loco = root / "TRAINS" / "TRAINSET" / "BNSF"
write(loco / "TESTLOCO.S", box_shape(3.0, 4.2, 20.0, "testloco.ace", base=0.2))
(loco / "TESTLOCO.ACE").write_bytes(ace(64, (200, 60, 30)))

# --- a 2D cab: one picture, no controls
write(loco / "CABVIEW" / "TESTLOCO.CVF", """SIMISA@@@@@@@@@@JINX0h0t______

Tr_CabViewFile (
	CabViewType ( 1 )
	CabViewFile ( frnt.ace )
	CabViewWindow ( 0 0 1024 768 )
	CabViewWindowFile ( "" )
	Position ( 0 3.2 9 )
	Direction ( 0 0 0 )
	EngineData ( testloco )
	CabViewControls ( 0 )
)
""")
(loco / "CABVIEW" / "FRNT.ACE").write_bytes(ace(256, (60, 90, 140), height=192, mipmaps=False))

# --- signal configuration: the route has no signals, but the simulator needs the file
write(route / "SIGCFG.DAT", """SIMISA@@@@@@@@@@JINX0g0t______

LightTextures ( 0 )
LightsTab ( 0 )
SignalTypes ( 0 )
SignalShapes ( 0 )
ScriptFiles ( 0 )
""")
# --- environment files: no water, no sky layers of its own; the simulator's defaults do the rest
for env in ("SUN.ENV", "RAIN.ENV", "SNOW.ENV"):
    write(route / "ENVFILES" / env, """SIMISA@@@@@@@@@@JINX0E0t______

world (
	world_water (
		world_water_wave_height ( 0 )
		world_water_wave_speed ( 0 )
		world_water_layers ( 0 )
	)
	world_sky (
		world_sky_layers ( 0 )
		world_sky_satellites ( 0 )
	)
)
""")
# --- track types, for the running sounds (the files they name need not exist)
write(route / "TTYPE.DAT", """SIMISA@@@@@@@@@@JINX0t1t______

1
TrackType ( "Default track" "InsideTrack.sms" "OutsideTrack.sms" )
""")
# --- route sounds: one scalability group, no streams
SMS = """SIMISA@@@@@@@@@@JINX0x1t______

Tr_SMS (
	ScalabiltyGroup ( 5
		Activation ( ExternalCam ( ) CabCam ( ) PassengerCam ( ) Distance ( 1000 ) )
		Deactivation ( Distance ( 1000 ) )
		Streams ( 0 )
	)
)
"""
for sms in ("INGAME.SMS", "INSIDETRACK.SMS", "OUTSIDETRACK.SMS"):
    write(route / "SOUND" / sms, SMS)
# --- the route file, activities, services and consists the launcher lists
write(route / "RIELTEST.TRK", f"""SIMISA@@@@@@@@@@JINX0r0t______

Tr_RouteFile (
	RouteID ( RIELTEST )
	Name ( "Riel Test Line" )
	Description ( "A small synthetic route: a straight line, two activities, two paths, and folders in capitals the way a copy off a Windows disk has them." )
	FileName ( RIELTEST )
	Electrified ( 00000000 )
	Environment (
		SpringClear ( sun.env ) SpringRain ( rain.env ) SpringSnow ( snow.env )
		SummerClear ( sun.env ) SummerRain ( rain.env ) SummerSnow ( snow.env )
		AutumnClear ( sun.env ) AutumnRain ( rain.env ) AutumnSnow ( snow.env )
		WinterClear ( sun.env ) WinterRain ( rain.env ) WinterSnow ( snow.env )
	)
	RouteStart ( {TX} {TZ} 0 0 )
	Graphic ( graphic.ace )
	LoadingScreen ( load.ace )
)
""")

def activity(name, title, description, briefing, start, season, weather, path, duration, difficulty, service):
    return f"""SIMISA@@@@@@@@@@JINX0a0t______

Tr_Activity (
	Serial ( 1 )
	Tr_Activity_Header (
		RouteID ( RIELTEST )
		Name ( "{title}" )
		Description ( "{description}" )
		Briefing ( "{briefing}" )
		CompleteActivity ( 1 )
		Type ( 0 )
		Mode ( 2 )
		StartTime ( {start} )
		Season ( {season} )
		Weather ( {weather} )
		PathID ( {path} )
		StartingSpeed ( 0 )
		Duration ( {duration} )
		Difficulty ( {difficulty} )
		Animals ( 0 )
		Workers ( 0 )
		FuelWater ( 100 )
		FuelCoal ( 100 )
		FuelDiesel ( 100 )
	)
	Tr_Activity_File (
		Player_Service_Definition ( {service}
			Player_Traffic_Definition ( 30600 )
		)
		NextServiceUID ( 1 )
		NextActivityObjectUID ( 32786 )
	)
)
"""

write(route / "ACTIVITIES" / "COAL.ACT", activity("COAL", "Morning coal run", "Take a loaded coal train from the mine down to the port.",
      "Depart at 08:30. Watch the grade after the tunnel; the train is heavy.", "8 30 0", 1, 0, "MAIN", "1 20", 1, "COALSRV"))
write(route / "ACTIVITIES" / "NIGHT.ACT", activity("NIGHT", "Night freight in the snow", "Bring the mixed freight back up the line overnight.",
      "Snow is forecast. Signals are spaced far apart on the upper section.", "22 15 0", 3, 1, "BRANCH", "0 45", 2, "NIGHTSRV"))

for service, path in (("COALSRV", "MAIN"), ("NIGHTSRV", "BRANCH")):
    write(route / "SERVICES" / f"{service}.SRV", f"""SIMISA@@@@@@@@@@JINX0v0t______

Service_Definition (
	Serial ( 1 )
	Name ( {service} )
	Train_Config ( TEST )
	PathID ( {path} )
	MaxWheelAcceleration ( 0 )
	Efficiency ( 0.9 )
	TimeTable (
		StartingSpeed ( 0 )
		EndingSpeed ( 0 )
		StartInWorld ( 0 )
		EndInWorld ( 0 )
	)
)
""")

for consist, title, speed in (("TEST", "Test Consist", "40.0"), ("COALHOP", "Coal hoppers, 12 cars", "25.0")):
    write(root / "TRAINS" / "CONSISTS" / f"{consist}.CON", f"""SIMISA@@@@@@@@@@JINX0D0t______

Train (
	TrainCfg ( "{consist}"
		Name ( "{title}" )
		Serial ( 1 )
		MaxVelocity ( {speed} 1.0 )
		NextWagonUID ( 1 )
		Durability ( 1.0 )
		Engine (
			UiD ( 0 )
			EngineData ( TESTLOCO BNSF )
		)
	)
)
""")

print("route written to", root)
