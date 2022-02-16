## Aura Controller

This project creates an API to interact with Asus' Aura RGB lights, it aims to minimize the bloatwares created by Asus' Aura software Installation.

### Minimum Requirements:
- Aura's LightingControlService
  - Aura SDK is included in LightingControlService
- HAL Device Drivers for your components/devices

### Usage
- Build and run the project
- Start interacting with API

### Types
```
GenericResponse {
    Message: string
}

ActiveResponse {
    active: boolean
}

AuraDeviceLight {
    Color: uint
    Name: string
    Red: uint
    Green: uint
    Blue uint
}

AuraDevice {
    Type: uint
    Name: string
    Height: uint
    Width: uint
    Lights AuraDeviceLight[]
}

ChangeColorRequest {
    Lights: uint[] | null // target light indexes; null means all
    Color: uint // color value
}
```

### API Endpoints    
- [GET]  http://localhost:54321/active 
  - determines if the server is in control
  - output: GenericResponse
- [POST] http://localhost:54321/activate
  - activate control; acquire control to aura
  - output: GenericResponse
- [POST] http://localhost:54321/deactivate
  - deactivate control; give up control to aura
  - output: GenericResponse
- [GET] http://localhost:54321/devices
  - returns available devices
  - output: AuraDevice[]
- [PUT] http://localhost:54321/devices/:device_name
  - sets device light color
  - input: ChangeColorRequest
  - output: AuraDevice