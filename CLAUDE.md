@AGENTS.md

# Claude Code specifics
- `.claude/rules/*.md` contains lazy-loaded, file-scoped rules; do not preload all rules.
- Project hooks silently synchronize Git metadata; do not invoke a second agent for memory.
