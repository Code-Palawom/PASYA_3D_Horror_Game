#!/bin/bash
set -uo pipefail

# Build Automation passes: $1 = project path, $2 = build output path, $3 = platform
UNITY_PROJECT_PATH="$1"

echo "Fetching dependencies..."

if [ -z "${DEPENDENCY_REPO_TOKEN:-}" ]; then
    echo "FAIL: DEPENDENCY_REPO_TOKEN is not set"
    exit 1
fi
if [ -z "${DEPENDENCY_REPO:-}" ]; then
    echo "FAIL: DEPENDENCY_REPO is not set"
    exit 1
fi
if [ -z "$UNITY_PROJECT_PATH" ]; then
    echo "FAIL: project path argument (\$1) is missing"
    exit 1
fi

echo "DEPENDENCY_REPO = ${DEPENDENCY_REPO}"
echo "DEPENDENCY_REPO_TOKEN length = ${#DEPENDENCY_REPO_TOKEN}"
echo "UNITY_PROJECT_PATH (from \$1) = ${UNITY_PROJECT_PATH}"
echo "git version: $(git --version)"

CLONE_DIR="$UNITY_PROJECT_PATH/CI_TempDependency"
LOG_FILE="$UNITY_PROJECT_PATH/CI_TempDependency.log"

echo "Using CLONE_DIR: $CLONE_DIR"
rm -rf "$CLONE_DIR" 2>/dev/null
rm -f "$LOG_FILE" 2>/dev/null

export GIT_TERMINAL_PROMPT=0
export GIT_ASKPASS=/bin/true

set +e
git clone --depth 1 --verbose --progress \
    "https://x-access-token:${DEPENDENCY_REPO_TOKEN}@${DEPENDENCY_REPO}" \
    "$CLONE_DIR" > "$LOG_FILE" 2>&1
CLONE_STATUS=$?
set -e

echo "git clone exit status: $CLONE_STATUS"
echo "----- clone log (redacted) -----"
sed "s/${DEPENDENCY_REPO_TOKEN}/***REDACTED***/g" "$LOG_FILE"
echo "----- end clone log -----"

if [ $CLONE_STATUS -ne 0 ]; then
    echo "FAIL: git clone returned non-zero exit code $CLONE_STATUS"
    exit 1
fi

# Verify via git itself instead of bash's [ -d ], since bash and git.exe
# can disagree on path resolution in this environment.
GIT_CHECK=$(git -C "$CLONE_DIR" rev-parse --is-inside-work-tree 2>&1)
if [ "$GIT_CHECK" != "true" ]; then
    echo "FAIL: git cannot verify '$CLONE_DIR' as a repo. git said: $GIT_CHECK"
    exit 1
fi

echo "Clone verified via git rev-parse. Listing files via git:"
git -C "$CLONE_DIR" ls-files

FAIL=0

copy_pair() {
    local src_rel="$1"
    local dest_rel="$2"
    local dest="$UNITY_PROJECT_PATH/$dest_rel"

    mkdir -p "$(dirname "$dest")"

    if ! git -C "$CLONE_DIR" show "HEAD:$src_rel" > "$dest" 2>/tmp/giterr.txt; then
        echo "FAIL: git could not extract $src_rel"
        cat /tmp/giterr.txt
        FAIL=1
        return
    fi
    if [ ! -s "$dest" ]; then
        echo "FAIL: $dest_rel extracted but is empty"
        FAIL=1
        return
    fi

    if ! git -C "$CLONE_DIR" show "HEAD:$src_rel.meta" > "$dest.meta" 2>/tmp/giterr.txt; then
        echo "FAIL: git could not extract $src_rel.meta"
        cat /tmp/giterr.txt
        FAIL=1
        return
    fi
    if [ ! -s "$dest.meta" ]; then
        echo "FAIL: $dest_rel.meta extracted but is empty"
        FAIL=1
        return
    fi
    if ! grep -q "^guid:" "$dest.meta"; then
        echo "FAIL: $dest_rel.meta has no guid"
        FAIL=1
        return
    fi

    echo "Synced: $dest_rel ($(wc -c < "$dest") bytes)"
}

copy_pair "google-services.json" "Assets/google-services.json"
copy_pair "Scripts/Firebase/Auth/AuthManager.cs" "Assets/Scripts/Firebase/Auth/AuthManager.cs"
copy_pair "Scripts/Firebase/Auth/DesktopGoogleAuth.cs" "Assets/Scripts/Firebase/Auth/DesktopGoogleAuth.cs"
copy_pair "Scripts/Firebase/SecretStore.cs" "Assets/Scripts/Firebase/SecretStore.cs"
copy_pair "Scripts/Misc/SaveEncryption.cs" "Assets/Scripts/Misc/SaveEncryption.cs"
copy_pair "Scripts/Misc/TipsManager.cs" "Assets/Scripts/Misc/TipsManager.cs"
copy_pair "Scripts/Misc/VersionChecker.cs" "Assets/Scripts/Misc/VersionChecker.cs"
copy_pair "Scripts/Multiplayer/LobbyManager.cs" "Assets/Scripts/Multiplayer/LobbyManager.cs"
copy_pair "Scripts/Multiplayer/RelayManager.cs" "Assets/Scripts/Multiplayer/RelayManager.cs"
copy_pair "Scripts/Quiz/QuestionSource/QuizFetcher.cs" "Assets/Scripts/Quiz/QuestionSource/QuizFetcher.cs"
copy_pair "Scripts/Quiz/QuestionSource/QuizRepository.cs" "Assets/Scripts/Quiz/QuestionSource/QuizRepository.cs"

rm -rf "$CLONE_DIR" 2>/dev/null
rm -f "$LOG_FILE" 2>/dev/null

if [ "$FAIL" -eq 1 ]; then
    echo "PRE-BUILD FAILED - dependency verification failed"
    exit 1
fi

echo "Dependency check complete: No missing packages found."

echo "----- Diagnostic: duplicate AuthManager.cs check -----"
find "$UNITY_PROJECT_PATH/Assets" -iname "AuthManager.cs" 2>/dev/null
find "$UNITY_PROJECT_PATH/Assets" -iname "AuthManager.cs.meta" -exec echo {} \; -exec cat {} \; 2>/dev/null

echo "----- Diagnostic: file content sanity check -----"
head -n 5 "$UNITY_PROJECT_PATH/Assets/Scripts/Firebase/Auth/AuthManager.cs"
echo "..."
grep -n "class AuthManager" "$UNITY_PROJECT_PATH/Assets/Scripts/Firebase/Auth/AuthManager.cs"

echo "----- Diagnostic: asmdef contents if any found -----"
for f in $(find "$UNITY_PROJECT_PATH/Assets" -iname "*.asmdef" 2>/dev/null); do
    echo "=== $f ==="
    cat "$f"
    echo ""
done

exit 0