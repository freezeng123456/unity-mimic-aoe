using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Frontier
{
    // Narrow, explicitly invoked copy migration for the 087 additive neutral
    // content only. It does not migrate authored scenarios, challenges or replays.
    public static class NeutralSaveMigration088
    {
        public const string LegacyAppVersion="0.25.0";
        public const string LegacyRulesHash="ad70350d6ed009c853117f4d4beafaa4c9d5d6bbd55e80da72dff02c07e4243b";
        static readonly string[] Units={"ally_river_pike","ally_highland_rifle","ally_coastal_marine"};
        static readonly string[] Techs={"ally_river_pike_drill","ally_highland_rifle_drill","ally_coastal_marine_drill"};
        static readonly string[] Allies={"river_guild","highland_covenant","coastal_league"};
        const string AddedFields=",\"additionalUnits\":[],\"additionalTechs\":[]";

        // Ordinary matches generate a canonical lowercase GUID. Restricting this
        // field also prevents an untrusted save from injecting path components
        // into report file names after the copied match is loaded.
        public static bool IsNormalMatchId(string value)
        {return value!=null&&Guid.TryParseExact(value,"N",out var parsed)&&parsed.ToString("N")==value;}

        // Reconstruct the exact old rules from the current catalog. The old hash
        // covers all old values and ordering, not only the known additions.
        public static RuleCatalog ReconstructPreviousRules(RuleCatalog current)
        {
            if(current?.entities==null||current.techs==null||current.neutralAllies==null||current.neutralAllies.Length!=3)
                throw new InvalidDataException("Neutral migration: unexpected current rules");
            var old=SnapshotCodec.Clone(current);
            if(old.entities.Length<Units.Length||old.techs.Length<Techs.Length)
                throw new InvalidDataException("Neutral migration: missing additions");
            for(int i=0;i<Units.Length;i++)
            {
                if(old.entities[old.entities.Length-Units.Length+i]?.id!=Units[i]||old.techs[old.techs.Length-Techs.Length+i]?.id!=Techs[i])
                    throw new InvalidDataException("Neutral migration: additions differ");
                var ally=old.neutralAllies[i];
                if(ally==null||ally.id!=Allies[i]||ally.additionalUnits==null||ally.additionalTechs==null||
                   ally.additionalUnits.Length!=1||ally.additionalUnits[0]!=Units[i]||ally.additionalTechs.Length!=1||ally.additionalTechs[0]!=Techs[i])
                    throw new InvalidDataException("Neutral migration: services differ");
                ally.additionalUnits=Array.Empty<string>();ally.additionalTechs=Array.Empty<string>();
            }
            Array.Resize(ref old.entities,old.entities.Length-Units.Length);
            Array.Resize(ref old.techs,old.techs.Length-Techs.Length);
            var modern=JsonUtility.ToJson(old);int count=0,at=0;
            while((at=modern.IndexOf(AddedFields,at,StringComparison.Ordinal))>=0){count++;at+=AddedFields.Length;}
            if(count!=3||SnapshotCodec.Hash(modern.Replace(AddedFields,""))!=LegacyRulesHash)
                throw new InvalidDataException("Neutral migration: previous rules do not match the trusted baseline");
            return old;
        }

        // Validates source state as old rules, then a copy as new rules.
        // Both files stay untouched on any validation error. Destination is always
        // newly created via File.Move; no existing slot is replaced.
        public static void CreateCopy(string source,string destination,RuleCatalog current,string expectedVersion)
        {
            if(string.IsNullOrWhiteSpace(source)||string.IsNullOrWhiteSpace(destination)||string.IsNullOrWhiteSpace(expectedVersion))
                throw new ArgumentException("Source, destination and exact game version are required");
            if(expectedVersion!=LegacyAppVersion||Application.version!=LegacyAppVersion)
                throw new InvalidDataException("Neutral migration is only available in the verified 0.25.0 build");
            source=Path.GetFullPath(source);destination=Path.GetFullPath(destination);
            if(string.Equals(source,destination,StringComparison.Ordinal)||File.Exists(destination))throw new IOException("Migration requires an unused destination");
            var oldRules=ReconstructPreviousRules(current);
            var saved=MatchSaveStore.Read(source,expectedVersion);
            if(!IsNormalMatchId(saved.matchId))throw new InvalidDataException("Checkpoint has an invalid match identity");
            if(saved.hasGuided||saved.guided!=null||saved.world.hasScenarioDocument||!string.IsNullOrEmpty(saved.world.scenarioHash)||saved.world.mapId==MapTopology.Scenario)
                throw new InvalidDataException("Challenges and authored scenarios require their original game version");
            if(saved.world.rulesHash!=LegacyRulesHash)throw new InvalidDataException("Save is not from the supported previous rules");
            var original=SnapshotCodec.Clone(saved.world);
            original.rulesHash=SnapshotCodec.RulesHash(oldRules);
            var oldWorld=World.Restore(oldRules,original);
            MatchSimulation.Restore(oldWorld,saved.simulation);
            var migrated=SnapshotCodec.Clone(saved);
            // JsonUtility creates an empty object for a null serializable class.
            // The prior eligibility check established that this is an ordinary save.
            migrated.hasGuided=false;migrated.guided=null;
            migrated.world.rulesHash=SnapshotCodec.RulesHash(current);
            var newWorld=World.Restore(current,migrated.world);
            MatchSimulation.Restore(newWorld,migrated.simulation);
            string dir=Path.GetDirectoryName(destination);
            if(!Directory.Exists(dir))throw new DirectoryNotFoundException("Destination directory must already exist");
            string temp=Path.Combine(dir,".neutral-migration-"+Guid.NewGuid().ToString("N"));
            try
            {
                MatchSaveStore.Write(temp,migrated);
                var readback=MatchSaveStore.Read(temp,expectedVersion);
                var verified=World.Restore(current,readback.world);MatchSimulation.Restore(verified,readback.simulation);
                // Compare the complete checkpoint, including camera, selection,
                // groups, audio and speed. Only the catalog identity may differ.
                readback.world.rulesHash=LegacyRulesHash;
                if(JsonUtility.ToJson(readback)!=JsonUtility.ToJson(saved))
                    throw new IOException("Migration readback did not match the original checkpoint");
                File.Move(temp,destination);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
