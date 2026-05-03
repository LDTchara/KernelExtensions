using System.Linq;
using Hacknet;
using KernelExtensions.AirCraft.Daemon;
using Pathfinder.Action;
using Pathfinder.Util;

public class UploadAircraftSysFile : DelayablePathfinderAction
{
    [XMLStorage]
    public string NodeID;

    [XMLStorage]
    public string Path;


    public override void Trigger(OS os)
    {
        Computer c = Programs.getComputer(os,NodeID);


        if (!string.IsNullOrEmpty(NodeID) && !(c==null))
        {
            if (FlightDaemon.CompToDamons.ContainsKey(c))
            {
                Folder ff = c.files.root.searchForFolder("FlightSystems");
                ff.files.Add(new FileEntry(PortExploits.ValidAircraftOperatingDLL, "747FlightOps.dll"));
            }
            else
            {
                Folder folderAtPath = Programs.getFolderAtPath(Path, os, c.files.root, returnsNullOnNoFind: true);

                FileEntry item = new(PortExploits.ValidAircraftOperatingDLL, "747FlightOps.dll");
                folderAtPath.files.Add(item);
            }


        }

    }

}