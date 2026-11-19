# 걸어다니는 시체 — 8방향

Built-in image_gen으로 생성. 원본 PNG의 알파를 보존하며, Unity import에서 400×400 영역 8개로 분할한다. Point 필터, 압축·밉맵 없음, 1600 PPU. 기존 프리팹의 루트 배율 4에서 프레임 한 변이 월드 1칸이다.

방향 순서: 아래, 위, 왼쪽, 오른쪽, 왼아래, 오른아래, 왼위, 오른위. 각 방향 1장. 대표 그림은 아래 방향이다. 이동과 공격 대상을 향할 때 그림을 갱신한다.

## 생성 프롬프트

Create a production-ready transparent PNG pixel art sprite sheet for a top-down 2D dungeon roguelike monster named Walking Corpse. Exactly EIGHT views of the SAME short squat zombie, arranged in a strict 4 columns by 2 rows equal-cell grid. Top row left to right: SOUTH facing viewer, NORTH showing back, WEST facing left in profile, EAST facing right in profile. Bottom row left to right: SOUTHWEST front-left three-quarter, SOUTHEAST front-right three-quarter, NORTHWEST back-left three-quarter, NORTHEAST back-right three-quarter. Each sprite is centered in its own cell with identical scale and feet baseline, generous transparent gap, no touching neighboring cells. Full body all limbs visible. Retro Japanese dungeon crawler 32x32 logical pixel sprite style, hard square pixel clusters, limited 16 color palette, dark deep plum outline, sickly desaturated green skin, bald skull with sparse charcoal hair, small glowing amber eyes, hunched posture arms dangling slightly forward, torn burgundy vest and ragged brown trousers, bare gray-green feet. Appealing readable low-level undead enemy, no gore. Orthographic consistent slightly overhead camera, no isometric floor. Each character about 26 pixels wide 29 pixels tall at logical resolution with 2 pixels padding in 32x32 cell. Output canvas aspect ratio 2:1, pixel edges crisp, all sprites same design. Transparent background with actual alpha, NO floor, NO shadows outside sprite, NO labels letters arrows border grids or decorations, NO extra poses. These are eight directional standing/walking-ready poses, not animation frames.


## 현재 적용: 대머리 시체 SD 버전

walking-corpse-hostile-sd-8-directions.png를 현재 몬스터 정의와 프리팹에 연결했다. 큰 대머리 두개골, 꺼진 눈구멍과 호박색 발광 눈, 드러난 이빨과 위협적인 손 자세로 적의 인상을 강화했다. 하이앵글 SD 비율 유지. 내장 image_gen 사용. 최종 프롬프트는 hostile-sd-prompt.md 참조.

420×420 슬라이스 8개, 1680 PPU, 프리팹 배율 4에서 한 프레임은 월드 1칸. 원본 PNG/알파 보존, Point 필터 및 무압축. 앞선 시트는 보관하되 게임 참조는 이 버전이다.
