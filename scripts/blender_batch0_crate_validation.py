import argparse
import hashlib
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy
import requests

import addon


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def serialize_file(path: Path) -> dict:
    return {
        "path": str(path),
        "exists": path.exists(),
        "size": path.stat().st_size if path.exists() else None,
        "sha256": sha256_of(path) if path.exists() else None,
    }


def get_sketchfab_headers() -> dict:
    api_key = getattr(bpy.context.scene, "blendermcp_sketchfab_api_key", "")
    if not api_key:
        raise RuntimeError("Sketchfab API key is not configured in the Blender scene.")
    return {"Authorization": f"Token {api_key}"}


def get_model_metadata(uid: str, headers: dict) -> dict:
    response = requests.get(
        f"https://api.sketchfab.com/v3/models/{uid}",
        headers=headers,
        timeout=30,
    )
    response.raise_for_status()
    data = response.json()
    return {
        "uid": uid,
        "name": data.get("name"),
        "viewer_url": data.get("viewerUrl"),
        "is_downloadable": data.get("isDownloadable"),
        "archives": data.get("archives"),
        "license": (data.get("license") or {}).get("label"),
        "vertex_count": data.get("vertexCount"),
        "face_count": data.get("faceCount"),
    }


def route_a_direct_probe(uid: str, target_path: Path, headers: dict) -> dict:
    result = {
        "route": "A-direct",
        "started_at_utc": utc_now(),
        "target_path": str(target_path),
        "target_format": "glb",
        "success": False,
        "manual_steps_required": [],
    }
    ensure_parent(target_path)
    if target_path.exists():
        target_path.unlink()

    try:
        metadata = get_model_metadata(uid, headers)
        result["model"] = metadata

        response = requests.get(
            f"https://api.sketchfab.com/v3/models/{uid}/download",
            headers=headers,
            timeout=30,
        )
        response.raise_for_status()
        download_info = response.json()
        result["download_manifest_keys"] = sorted(download_info.keys())

        glb_entry = download_info.get("glb") or {}
        glb_url = glb_entry.get("url")
        if not glb_url:
            result["failure_reason"] = "Direct GLB download URL not available from Sketchfab."
            result["failure_code"] = "F5"
            result["completed_at_utc"] = utc_now()
            return result

        glb_response = requests.get(glb_url, timeout=120)
        glb_response.raise_for_status()
        target_path.write_bytes(glb_response.content)

        result["success"] = True
        result["download_manifest"] = {
            "glb_size": glb_entry.get("size"),
            "glb_expires_seconds": glb_entry.get("expires"),
        }
        result["output"] = serialize_file(target_path)
        result["completed_at_utc"] = utc_now()
        return result
    except Exception as exc:
        result["failure_reason"] = str(exc)
        result["completed_at_utc"] = utc_now()
        return result


def deselect_all_objects() -> None:
    for obj in bpy.data.objects:
        try:
            obj.select_set(False)
        except Exception:
            pass


def route_b_blender_export(uid: str, target_path: Path) -> dict:
    result = {
        "route": "B-blender",
        "started_at_utc": utc_now(),
        "target_path": str(target_path),
        "target_format": "glb",
        "success": False,
        "manual_steps_required": [],
    }
    ensure_parent(target_path)
    if target_path.exists():
        target_path.unlink()

    try:
        deselect_all_objects()
        server = addon.BlenderMCPServer(port=bpy.context.scene.blendermcp_port)
        download_result = server.download_sketchfab_model(
            uid,
            normalize_size=False,
            target_size=1.0,
        )
        result["download"] = download_result

        if not download_result.get("success"):
            result["failure_reason"] = download_result.get("error") or download_result.get("message") or "Unknown Sketchfab import failure."
            result["completed_at_utc"] = utc_now()
            return result

        imported_names = set(download_result.get("imported_objects", []))
        deselect_all_objects()
        roots = []
        for obj in bpy.data.objects:
            if obj.name in imported_names:
                obj.select_set(True)
                if obj.parent is None:
                    roots.append(obj)

        if not bpy.context.selected_objects:
            result["failure_reason"] = "No imported objects were available for GLB export."
            result["completed_at_utc"] = utc_now()
            return result

        if roots:
            bpy.context.view_layer.objects.active = roots[0]

        export_result = bpy.ops.export_scene.gltf(
            filepath=str(target_path),
            export_format="GLB",
            use_selection=True,
        )
        result["export_result"] = list(export_result)

        if not target_path.exists():
            result["failure_reason"] = "GLB export finished without creating the target file."
            result["completed_at_utc"] = utc_now()
            return result

        result["success"] = True
        result["output"] = serialize_file(target_path)
        result["completed_at_utc"] = utc_now()
        return result
    except Exception as exc:
        result["failure_reason"] = str(exc)
        result["completed_at_utc"] = utc_now()
        return result


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []

    parser = argparse.ArgumentParser(description="Run Batch 0 crate route validation inside Blender.")
    parser.add_argument("--uid", required=True)
    parser.add_argument("--route-a-output", required=True)
    parser.add_argument("--route-b-output", required=True)
    parser.add_argument("--result-json", required=True)
    parser.add_argument("--candidate-pool", nargs="*", default=[])
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    route_a_output = Path(args.route_a_output).resolve()
    route_b_output = Path(args.route_b_output).resolve()
    result_json = Path(args.result_json).resolve()
    ensure_parent(result_json)

    headers = get_sketchfab_headers()
    summary = {
        "timestamp_utc": utc_now(),
        "source_platform": "Sketchfab",
        "source_uid": args.uid,
        "source_candidate_pool": args.candidate_pool,
        "scene_file": bpy.data.filepath,
        "routeA": route_a_direct_probe(args.uid, route_a_output, headers),
        "routeB": route_b_blender_export(args.uid, route_b_output),
    }

    result_json.write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
