"""
Mutation check: injects realistic bugs into the core logic one at a time and confirms the
test suite fails for each. A test that can't catch these isn't protecting anything.

    python tools/mutation_check.py
"""
import shutil, subprocess, os, sys
import tempfile, pathlib
SRC=str(pathlib.Path(__file__).resolve().parents[1]); WORK=os.path.join(tempfile.gettempdir(), 'soundsplit-mutants')
mutants = [
 ("render/capture interface swapped", "SoundSplit/Core/EndpointIds.cs", "(capture ? CaptureInterface : RenderInterface)", "(capture ? RenderInterface : CaptureInterface)"),
 ("only one interface id tried (the v1.0.0 bug)", "SoundSplit/Core/AudioPolicyConfig.cs", "            typeof(IAudioPolicyConfigFactoryDownlevel).GUID,\n        };", "        };"),
 ("Windows told again every tick", "SoundSplit/Core/Router.cs", "if (_applied.TryGetValue(key, out var current) && current == deviceId) continue;", ""),
 ("turning off doesn't reset apps", "SoundSplit/Core/Router.cs", "            if (!_policy.IsAvailable) return;", "            if (!_policy.IsAvailable || !enabled) return;"),
 ("refused call marked as applied", "SoundSplit/Core/Router.cs", "if (ok) _applied[key] = deviceId;", "_applied[key] = deviceId;"),
 ("dead pid gets reset", "SoundSplit/Core/Router.cs", "if (_isAlive(key.Pid))", "if (true)"),
 ("unplugged device still routed", "SoundSplit/Core/Router.cs", "var useOut = !string.IsNullOrEmpty(outId) && activeOutputs.Contains(outId);", "var useOut = !string.IsNullOrEmpty(outId);"),
 ("case-insensitive rule lookup lost on load", "SoundSplit/Core/Settings.cs", "s.Rules = new Dictionary<string, RouteRule>(s.Rules ?? new(), StringComparer.OrdinalIgnoreCase);", "s.Rules ??= new();"),
]
env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
caught=0
for name, f, old, new in mutants:
    shutil.rmtree(WORK, ignore_errors=True)
    os.makedirs(WORK)
    for d in ('SoundSplit/Core','tests/SoundSplit.Tests'):
        shutil.copytree(f'{SRC}/{d}', f'{WORK}/{d}', ignore=shutil.ignore_patterns('bin','obj'))
    p=f'{WORK}/{f}'; s=open(p).read()
    assert old in s, (name, old)
    open(p,'w').write(s.replace(old,new,1))
    r=subprocess.run(['dotnet','test',f'{WORK}/tests/SoundSplit.Tests'],capture_output=True,text=True,env=env)
    out=r.stdout+r.stderr
    failed=[l.strip().split(' ')[1] for l in out.splitlines() if l.strip().startswith('Failed SoundSplit')]
    ok = r.returncode!=0 and failed
    caught += bool(ok)
    print(f"{'CAUGHT ' if ok else 'MISSED '} {name}: " + (', '.join(x.split('.')[-1] for x in failed) if failed else out[-300:]))
print(f"\n{caught}/{len(mutants)} injected bugs caught")
sys.exit(0 if caught == len(mutants) else 1)
