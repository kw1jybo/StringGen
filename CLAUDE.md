@../.claude/rules/csharp.md

## Mutation Testing Branch Isolation

**This rule applies with NO EXCEPTIONS.**

If any work involves running a mutation testing tool (Stryker or equivalent), that work MUST be done on a branch that will not affect `master` or the current working branch:

- **If you are on `master`**: create a new branch for the mutation work before running Stryker.
- **If you are on a feature branch**: branch off that feature branch, do the mutation work on that subbranch, then merge the subbranch back when complete.

Never run Stryker on `master` or on a branch intended to accumulate feature work. A machine reboot, crash, or interrupted run must not leave the working branch in a dirty or inconsistent state.
