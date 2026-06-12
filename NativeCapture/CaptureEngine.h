#pragma once

#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <vector>
#include <cstdint>

struct MonitorInfo {
    HMONITOR hMonitor;
    int x, y, w, h;
    winrt::Windows::Graphics::Capture::GraphicsCaptureItem item{ nullptr };
};

class CaptureEngine {
public:
    CaptureEngine();
    ~CaptureEngine();

    bool Initialize();
    bool CaptureRegion(int x, int y, int w, int h, HMONITOR monitor, uint8_t*& outPixels, int& outWidth, int& outHeight);
    void Shutdown();

private:
    bool CreateD3D11Device();
    bool CreateCaptureItemForMonitor(HMONITOR hMonitor, winrt::Windows::Graphics::Capture::GraphicsCaptureItem& item);
    bool AcquireFrame(winrt::Windows::Graphics::Capture::GraphicsCaptureItem& item,
                     int rx, int ry, int cw, int ch, int mw, int mh,
                     uint8_t*& outPixels, int& outWidth, int& outHeight);

    winrt::com_ptr<ID3D11Device> m_d3dDevice;
    winrt::com_ptr<ID3D11DeviceContext> m_d3dContext;
    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice m_winrtDevice{ nullptr };
    std::vector<MonitorInfo> m_monitors;
    bool m_initialized = false;
};

// C exports for P/Invoke
extern "C" {
    __declspec(dllexport) bool __stdcall CaptureInitialize();
    __declspec(dllexport) bool __stdcall CaptureScreen(int x, int y, int w, int h, void* monitor, uint8_t** pixels, int* outW, int* outH);
    __declspec(dllexport) void __stdcall CaptureFreePixels(uint8_t* pixels);
    __declspec(dllexport) void __stdcall CaptureShutdown();
    __declspec(dllexport) const char* __stdcall CaptureGetLastError();
}
