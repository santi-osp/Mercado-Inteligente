import os
p = 'src/frontend/src/app/app.routes.ts'
c = open(p, 'r', encoding='utf-8').read()
target = "{ path: 'dev/ui-kit', component: UiKitPage, title: 'UI Kit (Dev)' },"
repl = "{ path: 'dev/ui-kit', component: UiKitPage, title: 'UI Kit (Dev)' },\n  { path: 'dev/matching', component: ComparePage, title: 'Matching (Dev)' },"
if '/dev/matching' not in c:
    c = c.replace(target, repl)
    open(p, 'w', encoding='utf-8').write(c)
    print('Updated app.routes.ts')
else:
    print('Already updated')
