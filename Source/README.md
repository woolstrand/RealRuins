# Real Ruins

Development considerations:
Since the mod initially was cobbled together in a couple of days, it's pretty messy inside. But it works and users demand new features, so
there are the following rules I try to follow to deliver product while trying to make it better from the inside.
Also please remember that C# is not my native language, so most likely there are many places where I could use much better approach.

working product > architecture. I need to keep this working, so architectural refactoring is an important task, but bugfixes have higher priority.
architecture > proper errors handling. Code is really messy, so for now I want to make it tidy and structured, and only after that do all "what if" stuff.
architecture > new features. Code state is at the point where it can be hardly extended. It needs refactoring and this is what I'm doing right now.
errors handling > optimization. 
speed > memory.

## To Do

## Done

## Updating instructions
1. Add supported version to About.xml
2. Add new folder for new version
3. Mention this folder in LoadFolders.xml
4. Update project settings to the new output folder for new version
5. Update postbuild.sh to copy from new output folder as well