# DESIGN_LOG.md — append-only source of truth

## Entry 001 — Original user intent

> Sửa lại thành v0.1.0. Đôi tên thành Xtension. Nâng cấp source code, tăng tốc độ, tối ưu và giảm tài nguyên tiêu thụ, tăng tính bền vững. Thử kết nối github push và tag release đa nền tảng [https://github.com/coderunknow/Xtension](https://github.com/coderunknow/Xtension) đảm bảo production ready, clean code.

## Entry 002 — Decisions

- Product and assembly identity: `Xtension`; semantic version: `0.1.0`, initial tag: `v0.1.0`.
- Preserve the app's Windows-only design because it depends on Win32, Windows Forms, Windows UI Automation, and `SendInput`. Provide architecture-specific Windows x86/x64 builds rather than claiming unsupported macOS/Linux compatibility.
- Keep the .NET Framework 4.8 target for compatibility with the existing source and simple local build path.
- Split application classes into focused source files without changing the safety model.
- Use a bounded 650 ms poll interval to lower repeated UI Automation scans; close-click handling still rescans the active tab immediately.
- Reduce scanner allocations by removing the per-node wrapper object and per-container child list. Use `StringBuilder` for diagnostics signatures.
- Fail closed when the active tab cannot be uniquely confirmed. Do not weaken this condition to improve apparent availability.
- CI compiles on Windows runners. A GitHub Release is triggered only by a pushed `v*` tag after the release workflow starts; this Linux environment cannot validate Windows UI behavior directly.

## Entry 003 — Progress and verification

- Done: renamed local project/source metadata and user-facing product strings to Xtension; centralized v0.1.0 and runtime settings; split source files; updated build scripts and manifest; wrote README/CHANGELOG; added CI and tag-release workflows; added a static consistency check.
- Done: inspected the old UIA scanner and preserved its maximum depth, node count, sibling guard, document-skip behavior, fail-closed selection rule, and fresh scan before sending Ctrl+W.
- Not done / blocked: Windows compilation and UI Automation runtime testing cannot be executed in this authoring environment. The production-ready claim remains gated on CI + manual Windows acceptance.
- Not done / pending connection result: push the repository contents and attempt to create the requested `v0.1.0` tag/release through the available GitHub connector actions; if tag/release creation is unsupported by the connector, report the exact boundary instead of claiming success.

## Entry 004 — Maintenance rule

Append new dated entries below; do not rewrite or delete prior entries. Preserve user intent and clearly distinguish done, not done, and blocked work.


## Entry 005 — GitHub write attempt

- 2026-10-14: repository metadata confirmed `coderunknow/Xtension`, default branch `main`, push permission reported by metadata, and existing MIT license.
- The available GitHub integration returned HTTP 403 `Resource not accessible by integration` for Git tree creation, branch creation, and Contents API file creation. No remote mutation occurred; no branch, source commit, tag, or release was created by the assistant.
- The authoring container cannot resolve `github.com`, so direct `git push` is unavailable here too.
- Added `publish-to-github.bat` for the user's authenticated Windows clone. It performs source validation, refuses an existing `v0.1.0` tag, pushes `main`, then creates/pushes the tag so the committed GitHub Actions release workflow can build/publish assets.


## Entry 006 — Release request for v0.1.0-dev1 (2026-10-14)

Verbatim user request (Vietnamese, unedited):

> Cho prompt để AI có kết nối github tiếp tục nâng cấp mã nguồn, thêm DESIGN_LOG, AGENTS_LOG,... và tiến hành push và tag release. Version: v0.1.0-dev1. đảm bảo hoạt động tốt, nhanh và mọi thứ hoàn hảo.

Decisions recorded from this request and the standing requirements:

- Version is `0.1.0-dev1` everywhere. The single source is the `VERSION` file. Tag `v0.1.0-dev1`. GitHub Release title `Xtension v0.1.0-dev1`. The release is a prerelease and is never marked latest.
- The overlay is a × on the selected horizontal Brave tab only. It moves with the tab and is hidden whenever the active tab is not confirmed.
- Brave's native close buttons are hidden through Brave's "Always hide tab close button" setting, if the installed Brave version provides it. This label is not confirmed in any source gathered so far, so the UI describes it as "Brave's tab close-button setting, if your Brave version provides it". Xtension never changes Brave's settings.
- Disabling the overlay and exiting the app are separate actions. Both warn that native close buttons may stay hidden if the Brave setting is still on.
- Startup with Windows is off by default. It is a per-user Run entry.
- Fail-closed rule: no keystroke unless a fresh scan of the active window confirms one selected horizontal tab and the click lands on its ×. Uncertain identity or geometry hides the button.
- Residual risk, documented and not mitigated: a tab switch in the roughly 100 ms between validation and keystroke cannot be prevented by any available API.
- Platform scope: Windows x86 and x64 only. No macOS or Linux claims. No placeholder binaries.
- Performance: no percentage claims. Diagnostics shows nodes visited and scan milliseconds so the on-device numbers can be measured later.

Status: source written and statically checked, not compiled or run in this environment. Nothing is claimed as tested until CI passes. GUI and Brave runtime behaviour is unverified.

Open item: the six earlier Vietnamese request quotes that the release plan calls for could not be restored verbatim from the session record. Only the original intent quote (Entry 001) and the request above are recorded here. Add the missing quotes by appending a new entry; do not edit this one.
