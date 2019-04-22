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

type PlanetData =
    {
        id:                 int;
        name:               string;
        solarSystemId:      int;
        position:           PositionData;
        typeId:             int;
    }

type AsteroidBeltData =
    {
        id:                 int;
        name:               string;
        solarSystemId:      int;
        position:           PositionData;
    }

type MoonData =
    {
        id:                 int;
        name:               string;
        solarSystemId:      int;
        position:           PositionData;
    }

type StationData =
    {
        id:                     int;
        name:                   string;
        solarSystemId:          int;
        position:               PositionData;
        typeId:                 int;
        services:               string[];
        maxDockableShipVolume:  int;
    }

type StarData= 
    {
        id:                     int;
        name:                   string;
        solarSystemId:          int;
        typeId:                 int;
    }

type StargateData=
    {
        id:                         int;
        name:                       string;
        solarSystemId:              int;
        position:                   PositionData;
        typeId:                     int;
        destinationSolarSystemId:   int;
        destinationStargateId:      int;
    }