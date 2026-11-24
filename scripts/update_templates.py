# update_templates.py
import base64, os, sys

def write_b64(path, b64_str):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, 'wb') as f:
        f.write(base64.b64decode(b64_str.encode('ascii')))
    print(f'Wrote {path}')

if __name__ == '__main__':
    print('Ready')
