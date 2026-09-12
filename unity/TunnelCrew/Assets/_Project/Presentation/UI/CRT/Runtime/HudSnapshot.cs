using System;
using TunnelCrew.Sim;

namespace TunnelCrew.Presentation.CRT
{
    /// <summary>Display contract, independent of Unity UI, from the current simulation frame.</summary>
    public readonly struct HudSnapshot
    {
        public readonly RoleId Role;
        public readonly double Hp, HpMax, Heat, AmmoFraction, ReloadSeconds, Dominance, Threat;
        public readonly int Ammo, Level, Xp, XpNeed, Depth;
        public readonly string Objective, Contract;
        public readonly bool Critical, HasGun, HasDrill, Reloading, Overheated, Downed;
        public HudSnapshot(TunnelSim sim)
        {
            var p=sim.Player; var b=sim.Build;
            Role=b.Role;Hp=p.Hp;HpMax=p.HpMax;Heat=p.DrillHeat;Ammo=b.Ammo;
            HasGun=b.RoleHasGun;HasDrill=b.RoleDigMul>0;Reloading=b.IsReloading;
            Overheated=p.DrillHeatLock>0;Downed=p.Downed;Critical=Hp/Math.Max(1,HpMax)<.22;
            AmmoFraction=Reloading?1-b.ReloadLeft/Math.Max(.001,b.ReloadTime):Ammo/(double)Math.Max(1,b.MagSize);
            ReloadSeconds=b.ReloadLeft;Level=sim.Xp.Level;Xp=sim.Xp.Xp;XpNeed=sim.Xp.XpNeed;Depth=sim.Depth;
            Dominance=sim.Objective!=null?sim.Objective.ProgressFraction(sim.Run):sim.Run.Dominance/Math.Max(.001,sim.Run.DominanceTarget);
            Objective=sim.Objective!=null?sim.Objective.HudText(sim.Run):$"암반 장악 {sim.Run.Dominance:P0} / {sim.Run.DominanceTarget:P0}";
            Contract=sim.Contract?.HudText??"";
            Threat=sim.Run.Threat;
        }
    }
}
