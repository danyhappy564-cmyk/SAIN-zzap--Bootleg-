#!/bin/bash
# 세션 시작/재개 시 이 저장소의 git hooks 경로를 추적되는 .githooks/로
# 강제한다. pre-commit이 매 커밋 직전 작성자를 R_F로 재확인/재설정하므로,
# 세션 도중 전역 git 설정이 흐트러져도 커밋 작성자가 틀어지지 않는다.
set -uo pipefail

cd "${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}" || exit 0

git rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0

git config core.hooksPath .githooks 2>/dev/null || true

exit 0
