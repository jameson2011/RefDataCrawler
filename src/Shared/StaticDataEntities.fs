namespace DotNetStaticData

        

type PositionData = 
    {
        x: float; 
        y: float; 
        z: float;
    } 

type RegionData = 
    { 
        id:                 int;
        name:               string;
        constellationIds:   int[];
    }

type ConstellationData =  
    {
        id:                 int;
        name:               string;
        regionId:           int;
        solarSystemIds:     int[];
        position:           PositionData;
    }

type SolarSystemData =
    {
        id:                 int;
        name:               string;
        constellationId:    int;
        position:           PositionData;
        secStatus:          float;
        stargateIds:        int[];
        stationIds:         int[];
        starIds:            int[];
        planetIds:          int[];
        beltIds:            int[];
        moonIds:            int[];
    }