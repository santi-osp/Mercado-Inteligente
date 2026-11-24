import sys, subprocess

if __name__ == '__main__':
    if len(sys.argv) > 1:
        with open(sys.argv[1], 'r', encoding='utf-8') as pf:
            content = pf.read()
    else:
        content = sys.stdin.read()
    res = subprocess.run(['codex.exe', '--codex-run-as-apply-patch', content], capture_output=True, text=True)
    if res.stdout:
        print(res.stdout, end='')
    if res.stderr:
        print(res.stderr, file=sys.stderr, end='')
    sys.exit(res.returncode)
