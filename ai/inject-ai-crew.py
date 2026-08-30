#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AI 크루 주입기
==============
게임 본편 HTML 한 벌에 ai/crew-ai.js 와 최소 훅을 심어 AI 디벨롭 파일을 만든다.

    python ai/inject-ai-crew.py                       # 최신 v*.html 자동 선택
    python ai/inject-ai-crew.py tunnel-crew-...html   # 대상 지정
    python ai/inject-ai-crew.py --out my-ai.html

게임 파일이 새 버전으로 올라가면 이 스크립트를 다시 돌리면 된다. 본편은
건드리지 않고 `-ai` 사본만 만든다.

훅은 전부 "AI 크루가 없으면 원본과 완전히 같은 코드 경로"가 되도록 짰다.
"""
import io
import os
import re
import sys
import glob

ROOT = os.path.dirname(os.path.abspath(os.path.join(__file__, '..')))
AI_DIR = os.path.join(ROOT, 'ai')
SRC_JS = os.path.join(AI_DIR, 'crew-ai.js')
MARK = 'AI_CREW_INJECTED_V1'


class PatchError(Exception):
    pass


def patch(text, old, new, label, count=1):
    """정확히 count 번 나타나야 한다. 아니면 게임 구조가 바뀐 것이므로 멈춘다."""
    n = text.count(old)
    if n != count:
        raise PatchError('[%s] 앵커를 %d번 찾음 (기대 %d)\n  ---\n  %s\n  ---'
                         % (label, n, count, old[:220]))
    return text.replace(old, new, count)


def newest_game_file():
    """대상 자동 선택 — mtime 이 아니라 버전 번호로 고른다.

    분기 파일(-node-polish 등)이 나중에 수정되는 일이 잦아서 mtime 은 신뢰할 수
    없다. 버전 번호가 가장 높은 '접미사 없는' 본선 파일을 고르고, 동률이면
    최근 수정본을 쓴다. 어느 파일을 골랐는지는 항상 출력한다.
    """
    cands = [p for p in glob.glob(os.path.join(ROOT, 'tunnel-crew-infinite-mode-v*.html'))
             if '-ai.html' not in p]
    if not cands:
        raise SystemExit('본편 HTML을 찾지 못했습니다.')

    def key(path):
        name = os.path.basename(path)
        m = re.match(r'tunnel-crew-infinite-mode-v(\d+)\.(\d+)\.(\d+)(.*)\.html$', name)
        if not m:
            return (0, 0, 0, 0, os.path.getmtime(path))
        major, minor, patch_no, suffix = int(m.group(1)), int(m.group(2)), int(m.group(3)), m.group(4)
        plain = 1 if suffix == '' else 0        # 접미사 없는 본선을 우선
        return (major, minor, patch_no, plain, os.path.getmtime(path))

    return max(cands, key=key)


def main():
    args = [a for a in sys.argv[1:]]
    out_path = None
    if '--out' in args:
        i = args.index('--out')
        out_path = args[i + 1]
        del args[i:i + 2]
    target = args[0] if args else newest_game_file()
    if not os.path.isabs(target):
        target = os.path.join(ROOT, target)
    if out_path is None:
        out_path = re.sub(r'\.html$', '-ai.html', target)
    if not os.path.isabs(out_path):
        out_path = os.path.join(ROOT, out_path)

    s = io.open(target, encoding='utf-8').read()
    if MARK in s:
        raise SystemExit('이미 AI 크루가 주입된 파일입니다: ' + target)
    js = io.open(SRC_JS, encoding='utf-8').read()

    # ──────────────────────────────────────────────────────────────
    # 1) 적의 타깃 — 사람 고정에서 "가장 가까운 크루"로.
    #    AI_TGT() 는 크루가 없으면 G.sh 를 그대로 돌려주므로 원본과 동일하다.
    # ──────────────────────────────────────────────────────────────
    s = patch(s,
              "    const dx=sh.x-e.x, dy=sh.y-e.y, dist=Math.hypot(dx,dy)||1;",
              "    const tgt=(typeof AI_TGT==='function')?AI_TGT(e):sh;   /* AI 크루 — 가장 가까운 크루를 노린다 */\n"
              "    const dx=tgt.x-e.x, dy=tgt.y-e.y, dist=Math.hypot(dx,dy)||1;",
              'updateEnemies: 타깃 거리')

    s = patch(s,
              "    if(e.boss && dist<touch && e.cd<=0 && e.hp>0){\n"
              "      e.cd=DEMO.enemyAtkCd;\n"
              "      enemyContactDamage(e,dx/dist,dy/dist,1);\n"
              "    }",
              "    if(e.boss && dist<touch && e.cd<=0 && e.hp>0){\n"
              "      e.cd=DEMO.enemyAtkCd;\n"
              "      enemyContactDamage(e,dx/dist,dy/dist,1);\n"
              "    }",
              'updateEnemies: 접촉(변경 없음 · 존재 확인)')

    s = patch(s,
              "function updateEnemyAI(e,dt,spd,timeSpd,dist,dx,dy){\n const sh=G.sh;",
              "function updateEnemyAI(e,dt,spd,timeSpd,dist,dx,dy){\n"
              " const sh=(typeof AI_TGT==='function')?AI_TGT(e):G.sh;   /* AI 크루 타깃 */",
              'updateEnemyAI: 타깃')

    s = patch(s,
              "function enemySeesPlayer(e){\n const sh=G.sh,",
              "function enemySeesPlayer(e){\n const sh=(typeof AI_TGT==='function')?AI_TGT(e):G.sh,",
              'enemySeesPlayer: 타깃')

    s = patch(s,
              "function enemyMeleeStrike(e){\n const sh=G.sh,",
              "function enemyMeleeStrike(e){\n const sh=(typeof AI_TGT==='function')?AI_TGT(e):G.sh,",
              'enemyMeleeStrike: 타깃')

    s = patch(s,
              "function enemyFireShot(e){\n const sh=G.sh,",
              "function enemyFireShot(e){\n const sh=(typeof AI_TGT==='function')?AI_TGT(e):G.sh,",
              'enemyFireShot: 타깃')

    # 근접 사거리 판정은 R_SHELLY 기준이라 AI 크루에도 그대로 쓸 수 있다.
    # 실제 피해만 타깃으로 분기한다.
    s = patch(s,
              " return applyPlayerDamage(dmg*(mul==null?1:mul),nx,ny);",
              " /* AI 크루가 맞은 경우 그쪽 체력을 깎는다 — 사람은 기존 경로 그대로 */\n"
              " if(typeof AI_TGT_HURT==='function'){const r=AI_TGT_HURT(e,dmg*(mul==null?1:mul),nx,ny);if(r!==null)return r;}\n"
              " return applyPlayerDamage(dmg*(mul==null?1:mul),nx,ny);",
              'enemyContactDamage: AI 분기')

    # 적 투사체도 AI 크루를 맞힌다
    s = patch(s,
              "  if(!dead&&Math.hypot(s.x-sh.x,s.y-sh.y)<R_SHELLY+s.r){\n"
              "   const d=Math.hypot(s.vx,s.vy)||1;\n"
              "   if(applyPlayerDamage(s.dmg,s.vx/d,s.vy/d)){dead=true;J.burst(s.x,s.y,8,['#8BE38F','#FFF'],140);}\n"
              "  }",
              "  if(!dead&&typeof AICREW!=='undefined'&&AICREW.members.length){\n"
              "   const hitM=AICREW.hitTest(s.x,s.y,s.r);\n"
              "   if(hitM){const d=Math.hypot(s.vx,s.vy)||1;\n"
              "    if(AICREW.hurt(hitM,s.dmg,s.vx/d,s.vy/d)){dead=true;J.burst(s.x,s.y,8,['#8BE38F','#FFF'],140);}}\n"
              "  }\n"
              "  if(!dead&&Math.hypot(s.x-sh.x,s.y-sh.y)<R_SHELLY+s.r){\n"
              "   const d=Math.hypot(s.vx,s.vy)||1;\n"
              "   if(applyPlayerDamage(s.dmg,s.vx/d,s.vy/d)){dead=true;J.burst(s.x,s.y,8,['#8BE38F','#FFF'],140);}\n"
              "  }",
              'updateEnemyShots: AI 피격')

    # ──────────────────────────────────────────────────────────────
    # 2) AI 가 부순 블록 — 장악도에만 기여, 사람 경험치·특성 발동은 없다 (§5.2)
    # ──────────────────────────────────────────────────────────────
    s = patch(s,
              "  if(typeof INF!=='undefined'&&infTraitCardsEnabled()&&typeof infOnBlockBroken==='function')infOnBlockBroken(t,x,y);",
              "  if(typeof INF!=='undefined'&&infTraitCardsEnabled()&&typeof infOnBlockBroken==='function'){\n"
              "   /* AI 크루가 부순 블록은 장악도만 올린다 — 개인 성장은 사람만 (§5.2) */\n"
              "   if(typeof AICREW!=='undefined'&&AICREW.breakSrc)AICREW.creditBreak(t,x,y);\n"
              "   else infOnBlockBroken(t,x,y);\n"
              "  }",
              'damage: AI 파괴 분기')

    # ──────────────────────────────────────────────────────────────
    # 3) AI 투사체는 사람의 특성 배율을 타지 않는다
    # ──────────────────────────────────────────────────────────────
    s = patch(s,
              "    const gm=(typeof crewGunMul==='function')?crewGunMul():1;\n"
              "    const im=(typeof INF!=='undefined'&&INF.active)?INF.gunMul:1;",
              "    /* AI 크루의 탄은 AI 자신의 배율로 계산한다 — 사람 빌드와 분리 */\n"
              "    const gm=p.ai?1:((typeof crewGunMul==='function')?crewGunMul():1);\n"
              "    const im=p.ai?(p.aiMul||1):((typeof INF!=='undefined'&&INF.active)?INF.gunMul:1);\n"
              "    /* 처치 경험치를 쏜 크루에게 귀속시킨다 */\n"
              "    if(p.ai&&typeof AICREW!=='undefined')AICREW.dmgSrc=AICREW.ownerOf(p);",
              'updateProjectiles: AI 배율')

    s = patch(s,
              "    hurtEnemy(e,DEMO.enemyGunDmg*gm*im*(p.power||1)*(p.laser?1.65:1),nx,ny,p.srcTurret?'turret':'weapon');",
              "    try{hurtEnemy(e,DEMO.enemyGunDmg*gm*im*(p.power||1)*(p.laser?1.65:1),nx,ny,p.srcTurret?'turret':'weapon');}\n"
              "    finally{if(typeof AICREW!=='undefined')AICREW.dmgSrc=null;}",
              'updateProjectiles: 처치 귀속 해제')

    s = patch(s,
              "    const k=ci(c,r),im=(typeof INF!=='undefined'&&INF.active)?INF.gunWallMul:1;\n"
              "    if(k!==p.lastCell){damage(c,r,shelDps()*.28*im*(p.power||1)*(p.laser?1.8:1),nx,ny,false);p.lastCell=k;}",
              "    const k=ci(c,r),im=p.ai?1:((typeof INF!=='undefined'&&INF.active)?INF.gunWallMul:1);\n"
              "    if(k!==p.lastCell){\n"
              "     if(p.ai&&typeof AICREW!=='undefined')AICREW.breakSrc=p;\n"
              "     try{damage(c,r,shelDps()*.28*im*(p.power||1)*(p.laser?1.8:1),nx,ny,false);}\n"
              "     finally{if(p.ai&&typeof AICREW!=='undefined')AICREW.breakSrc=null;}\n"
              "     p.lastCell=k;}",
              'updateProjectiles: AI 벽 피해')

    # ──────────────────────────────────────────────────────────────
    # 3.5) 적 처치 경험치 — AI 가 잡았으면 그 크루의 개인 경험치로 (§5.2)
    # ──────────────────────────────────────────────────────────────
    s = patch(s,
              "     if(typeof INF!=='undefined'&&INF.active&&typeof infAwardEnemyKillXp==='function')infAwardEnemyKillXp(e,src);",
              "     /* AI 크루가 낸 피해로 죽었으면 그 크루가 경험치를 가져간다 (§5.2) */\n"
              "     if(typeof AICREW!=='undefined'&&AICREW.dmgSrc)AICREW.awardKill(e,src);\n"
              "     else if(typeof INF!=='undefined'&&INF.active&&typeof infAwardEnemyKillXp==='function')infAwardEnemyKillXp(e,src);",
              'hurtEnemy: 처치 경험치 귀속')

    # ──────────────────────────────────────────────────────────────
    # 3.6) 레벨업 카드 비정지 — AI 크루가 있으면 코옵과 같은 규칙 (§17.2-5)
    #      실제 코옵에서는 서로 카드 뽑는 시점이 달라 월드가 멈추지 않는다.
    #      AI 와 플레이할 때도 같아야 한다.
    # ──────────────────────────────────────────────────────────────
    s = patch(s,
              " const coopLive=typeof COOP!=='undefined'&&COOP.active&&COOP._started;",
              " const aiLive=typeof AICREW!=='undefined'&&AICREW.enabled&&AICREW.members.length>0;\n"
              " /* AI 크루가 있으면 월드를 멈추지 않는다 — 선택은 1·2·3 키 (§17.2-5) */\n"
              " const coopLive=(typeof COOP!=='undefined'&&COOP.active&&COOP._started)||aiLive;",
              'infOpenLevel: 비정지 조건')

    # ──────────────────────────────────────────────────────────────
    # 3.7) 보스 투사체 — AI 크루도 맞는다.
    #      사람만 맞으면 AI 의 회피 행동이 무의미해진다.
    # ──────────────────────────────────────────────────────────────
    s = patch(s,
              " const d=Math.hypot(G.sh.x-s.tx,G.sh.y-s.ty);if(d>s.rad||G.iframes>0)return;",
              " if(typeof AICREW!=='undefined')AICREW.bossShotHit(s);   /* AI 크루 피격 */\n"
              " const d=Math.hypot(G.sh.x-s.tx,G.sh.y-s.ty);if(d>s.rad||G.iframes>0)return;",
              'infBossProjectileHit: AI 크루 피격')

    # ──────────────────────────────────────────────────────────────
    # 4) 프레임 훅 — 갱신 / 렌더
    # ──────────────────────────────────────────────────────────────
    s = patch(s,
              " if(typeof updateCrew==='function')updateCrew(dt);",
              " if(typeof updateCrew==='function')updateCrew(dt);\n"
              " if(typeof AICREW!=='undefined')AICREW.update(dt);   /* AI 크루 */",
              'update: AI 크루 갱신')

    s = patch(s,
              " if(typeof COOP_drawPeer==='function')COOP_drawPeer();",
              " if(typeof AICREW!=='undefined')AICREW.draw();   /* AI 크루 */\n"
              " if(typeof COOP_drawPeer==='function')COOP_drawPeer();",
              'renderDepths: AI 크루 렌더')

    # 시야 합산 — AI 크루도 어둠을 연다
    s = patch(s,
              "   const peerList=(typeof COOP_peersXY==='function'&&COOP_peersXY())\n"
              "    ||(typeof COOP_peerXY==='function'&&COOP_peerXY()?[COOP_peerXY()]:null);",
              "   let peerList=(typeof COOP_peersXY==='function'&&COOP_peersXY())\n"
              "    ||(typeof COOP_peerXY==='function'&&COOP_peerXY()?[COOP_peerXY()]:null);\n"
              "   /* AI 크루 시야 합산 — 동료가 비춘 곳은 팀 전체가 본다 */\n"
              "   {const ai=(typeof AICREW!=='undefined')&&AICREW.visionXY();\n"
              "    if(ai)peerList=peerList?peerList.concat(ai):ai;}",
              'LOS: AI 크루 시야')

    # ──────────────────────────────────────────────────────────────
    # 5) 런 생명주기
    # ──────────────────────────────────────────────────────────────
    s = patch(s,
              " infInitFloor();toast(role.name+' · 무한 모드 시작');infPaint();",
              " infInitFloor();toast(role.name+' · 무한 모드 시작');infPaint();\n"
              " if(typeof AICREW!=='undefined')AICREW.onRunStart();   /* AI 크루 편성 투입 */",
              'infStartRun: AI 크루 투입')

    s = patch(s,
              " toast(infDepthLabel(INF.depth)+' — '+(infIsAbyss(INF.depth)",
              " if(typeof AICREW!=='undefined'&&AICREW.members.length)AICREW.onFloorInit();   /* 새 지층에서 크루 재집결 */\n"
              " toast(infDepthLabel(INF.depth)+' — '+(infIsAbyss(INF.depth)",
              'infInitFloor: AI 크루 재집결')

    s = patch(s,
              "function infEndRun(reason,escaped){\n if(!INF.active||CREW.phase==='infiniteResult')return;",
              "function infEndRun(reason,escaped){\n if(!INF.active||CREW.phase==='infiniteResult')return;\n"
              " if(typeof AICREW!=='undefined')AICREW.onRunEnd();   /* AI 크루 정리 */",
              'infEndRun: AI 크루 정리')

    # 미션 모드(startCrewMission)도 지원한다
    if 'function startCrewMission(' in s:
        m = re.search(r"function startCrewMission\(([^)]*)\)\{", s)
        if m:
            head = m.group(0)
            s = patch(s, head, head + "\n /* AI 크루 — 미션 모드에서도 같이 나간다 */\n"
                                     " setTimeout(()=>{if(typeof AICREW!=='undefined')AICREW.onRunStart();},0);",
                      'startCrewMission: AI 크루 투입')

    # ──────────────────────────────────────────────────────────────
    # 5.5) 탈출 — 생존자 전원 탑승에 AI 크루를 포함한다 (§8.4-3·4)
    #      AI 크루가 없으면 aboard 가 null 로 남아 기존 솔로 판정 그대로다.
    # ──────────────────────────────────────────────────────────────
    s = patch(s,
              " if(esc.localBoarded){\n"
              "  if(typeof COOP_escapeAllAboard==='function'&&COOP_escapeAllAboard()===true){",
              " if(esc.localBoarded){\n"
              "  /* AI 크루도 다 타야 로켓이 뜬다 — 혼자 못 나간다 */\n"
              "  if(typeof AICREW!=='undefined'&&AICREW.escapeAllAboard()===true){\n"
              "   J.flash(esc.x,esc.y,CELL*2,'rgba(255,211,110,.9)');infEndRun('크루 전원 탑승',true);return;\n"
              "  }\n"
              "  if(typeof COOP_escapeAllAboard==='function'&&COOP_escapeAllAboard()===true){",
              'infUpdateEscape: AI 크루 대기')

    s = patch(s,
              "   const aboard=(typeof COOP_escapeAllAboard==='function')?COOP_escapeAllAboard():null;\n"
              "   if(aboard===null){J.flash(esc.x,esc.y,CELL*2,'rgba(255,211,110,.9)');infEndRun('탈출 포트 탑승',true);}   /* 솔로 — 기존 판정 유지 */",
              "   const aboard=(typeof COOP_escapeAllAboard==='function')?COOP_escapeAllAboard():null;\n"
              "   /* AI 크루가 있으면 솔로여도 '생존자 전원 탑승' 판정을 쓴다 */\n"
              "   if(aboard===null&&typeof AICREW!=='undefined'){\n"
              "    const a=AICREW.escapeAllAboard();\n"
              "    if(a!==null){\n"
              "     esc.localBoarded=true;\n"
              "     if(a===true){J.flash(esc.x,esc.y,CELL*2,'rgba(255,211,110,.9)');infEndRun('크루 전원 탑승',true);}\n"
              "     else{const cc=AICREW.escapeCount();toast('탑승 완료 — 크루를 기다린다 ('+(cc.boarded+1)+'/'+cc.total+')');}\n"
              "     return;\n"
              "    }\n"
              "   }\n"
              "   if(aboard===null){J.flash(esc.x,esc.y,CELL*2,'rgba(255,211,110,.9)');infEndRun('탈출 포트 탑승',true);}   /* 솔로 — 기존 판정 유지 */",
              'infUpdateEscape: AI 크루 전원 탑승')

    # ──────────────────────────────────────────────────────────────
    # 6) 본체 삽입
    # ──────────────────────────────────────────────────────────────
    block = ('\n<!-- ' + MARK + ' — ai/crew-ai.js 에서 생성. 원본을 고치려면 그 파일을 고치고\n'
             '     python ai/inject-ai-crew.py 를 다시 실행하세요. -->\n'
             '<script>\n' + js + '\n</script>\n')
    s = patch(s, '</body>', block + '</body>', '본체 삽입')

    io.open(out_path, 'w', encoding='utf-8', newline='').write(s)
    print('원본 :', os.path.relpath(target, ROOT))
    print('생성 :', os.path.relpath(out_path, ROOT))
    print('크기 :', '%.1f MB' % (len(s.encode('utf-8')) / 1048576.0))


if __name__ == '__main__':
    try:
        main()
    except PatchError as e:
        print('주입 실패 — 게임 코드 구조가 바뀌었습니다.\n' + str(e), file=sys.stderr)
        sys.exit(1)
