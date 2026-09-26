"""Serve the map monitor without starting a game server (optional saved /api/state JSON).

Stop this preview before starting a natural run on the same dashboard port.
"""
import argparse
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=17880)
    parser.add_argument('--snapshot', type=Path, help='Saved response from the running monitor /api/state endpoint')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2] / 'tests/Aion.Bots/Dashboard'
    saved = json.loads(args.snapshot.read_text(encoding='utf-8-sig')) if args.snapshot else {}
    assets = {'/' + p.relative_to(root).as_posix(): p for p in root.rglob('*')
              if p.suffix in ('.html', '.css', '.js', '.json', '.webp')}
    assets['/'] = root / 'index.html'
    types = {'.html': 'text/html', '.css': 'text/css', '.js': 'text/javascript',
             '.json': 'application/json', '.webp': 'image/webp'}

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            route = self.path.split('?', 1)[0]
            if route == '/api/state':
                data = {**saved, 'mode': 'preview', 'run': saved.get('run', 'Map preview'),
                        'scenarios': saved.get('scenarios', []), 'bots': saved.get('bots', []),
                        'serverTime': datetime.now(timezone.utc).isoformat()}
                body, content_type = json.dumps(data).encode(), 'application/json'
            elif route in assets:
                body, content_type = assets[route].read_bytes(), types[assets[route].suffix]
            else:
                self.send_error(404)
                return
            self.send_response(200)
            self.send_header('Content-Type', content_type + ('; charset=utf-8' if content_type != 'image/webp' else ''))
            self.send_header('Content-Length', str(len(body)))
            self.send_header('Cache-Control', 'no-store')
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.send_header('Content-Security-Policy', "default-src 'self'; connect-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self'; img-src 'self'")
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *_):
            pass

    print(f'Map preview: http://127.0.0.1:{args.port}/ (no active bot)', flush=True)
    with ThreadingHTTPServer(('127.0.0.1', args.port), Handler) as server:
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == '__main__':
    main()
