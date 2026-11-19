# -*- coding: utf-8 -*-
"""把 Category:COSMOS missions 的页面清单存下来。

用途：这些页面的 wiki 分类信息不全，抓取时全都落到了"安全任务"，
构建时用这份清单把它们改成 COSMOS（界面上叫「空中代理人」）。
"""

import io
import json
import os
import sys
import urllib.parse
import urllib.request

API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0'}
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cosmos_missions.json')


def members(cat):
    out, cont = [], None
    while True:
        params = {'action': 'query', 'list': 'categorymembers', 'cmtitle': 'Category:' + cat,
                  'cmlimit': '500', 'cmtype': 'page', 'format': 'json'}
        if cont:
            params['cmcontinue'] = cont
        url = API + '?' + urllib.parse.urlencode(params)
        data = json.loads(urllib.request.urlopen(
            urllib.request.Request(url, headers=UA), timeout=60).read().decode('utf-8'))
        out += [m['title'] for m in data.get('query', {}).get('categorymembers', [])]
        cont = data.get('continue', {}).get('cmcontinue')
        if not cont:
            break
    return out


def main():
    titles = sorted(members('COSMOS missions'))
    json.dump(titles, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('COSMOS 任务页 %d 个 → %s' % (len(titles), OUT))
    print('样例：', ' | '.join(titles[:6]))


if __name__ == '__main__':
    sys.exit(main())
