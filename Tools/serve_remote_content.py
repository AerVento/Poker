"""Serve built artwork for local verification: python Tools/serve_remote_content.py."""
import argparse
import functools
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class ContentHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Expose-Headers", "Content-Length, ETag, Last-Modified")
        self.send_header("Cache-Control", "public, max-age=31536000, immutable"
                         if self.path.split("?")[0].endswith(".bundle") else "no-cache")
        super().end_headers()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1] / "Build/RemoteContent")
    parser.add_argument("--port", type=int, default=18080)
    parser.add_argument("--bind", default="127.0.0.1")
    args = parser.parse_args()
    root = args.root.resolve(strict=True)
    server = ThreadingHTTPServer((args.bind, args.port), functools.partial(ContentHandler, directory=str(root)))
    print(f"Serving {root} at http://{args.bind}:{args.port}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
