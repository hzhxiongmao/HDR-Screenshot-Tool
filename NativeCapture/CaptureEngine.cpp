#include "CaptureEngine.h"
#include <windows.graphics.directx.direct3d11.interop.h>
#include <cmath>
#include <algorithm>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")

static std::string g_lastError;

namespace wgc = winrt::Windows::Graphics::Capture;
namespace wgd = winrt::Windows::Graphics::DirectX;
namespace wf = winrt::Windows::Foundation;

static CaptureEngine* g_engine = nullptr;

CaptureEngine::CaptureEngine() {}
CaptureEngine::~CaptureEngine() { Shutdown(); }

bool CaptureEngine::Initialize()
{
    return CreateD3D11Device();
}

bool CaptureEngine::CreateD3D11Device()
{
    UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
    D3D_FEATURE_LEVEL levels[] = { D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_1 };
    D3D_FEATURE_LEVEL fl;

    HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
        levels, ARRAYSIZE(levels), D3D11_SDK_VERSION,
        m_d3dDevice.put(), &fl, m_d3dContext.put());
    if (FAILED(hr)) return false;

    winrt::com_ptr<IDXGIDevice> dxgiDevice;
    m_d3dDevice.as(dxgiDevice);

    winrt::com_ptr<::IInspectable> inspectable;
    hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.get(), inspectable.put());
    if (FAILED(hr)) return false;

    m_winrtDevice = inspectable.as<wgd::Direct3D11::IDirect3DDevice>();
    m_initialized = true;
    return true;
}

bool CaptureEngine::CreateCaptureItemForMonitor(HMONITOR hMonitor, wgc::GraphicsCaptureItem& item)
{
    auto interopFactory = winrt::get_activation_factory<wgc::GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
    winrt::com_ptr<::IInspectable> inspectable;
    HRESULT hr = interopFactory->CreateForMonitor(hMonitor, winrt::guid_of<wgc::GraphicsCaptureItem>(),
        reinterpret_cast<void**>(winrt::put_abi(inspectable)));
    if (FAILED(hr)) { g_lastError = "CreateForMonitor failed: 0x" + std::to_string(hr); return false; }
    item = inspectable.as<wgc::GraphicsCaptureItem>();
    return true;
}

bool CaptureEngine::CaptureRegion(int x, int y, int w, int h, HMONITOR monitor,
    uint8_t*& outPixels, int& outWidth, int& outHeight)
{
    if (!m_initialized) { g_lastError = "Not initialized"; return false; }

    wgc::GraphicsCaptureItem item{ nullptr };

    // Try to find cached item
    for (auto& mi : m_monitors) {
        if (mi.hMonitor == monitor && mi.item) {
            item = mi.item;
            break;
        }
    }

    // Create new item if not cached
    if (!item) {
        if (!CreateCaptureItemForMonitor(monitor, item)) return false;
        MonitorInfo mi;
        mi.hMonitor = monitor;
        mi.item = item;
        m_monitors.push_back(mi);
    }

    // Get monitor dimensions
    MONITORINFOEXW mi = { sizeof(mi) };
    if (!GetMonitorInfoW(monitor, &mi)) { g_lastError = "GetMonitorInfo failed"; return false; }
    int monW = mi.rcMonitor.right - mi.rcMonitor.left;
    int monH = mi.rcMonitor.bottom - mi.rcMonitor.top;

    // Calculate relative region
    int relX = x - mi.rcMonitor.left;
    int relY = y - mi.rcMonitor.top;
    int capW = min(w, monW - relX);
    int capH = min(h, monH - relY);
    if (relX < 0 || relY < 0) {
        g_lastError = "Region outside monitor: relX=" + std::to_string(relX) + " relY=" + std::to_string(relY) +
                     " mon=(" + std::to_string(mi.rcMonitor.left) + "," + std::to_string(mi.rcMonitor.top) + ")";
        return false;
    }
    if (capW <= 0 || capH <= 0) {
        g_lastError = "Invalid size: capW=" + std::to_string(capW) + " capH=" + std::to_string(capH);
        return false;
    }

    return AcquireFrame(item, relX, relY, capW, capH, monW, monH, outPixels, outWidth, outHeight);
}

static float HalfToFloat(uint16_t half)
{
    uint32_t h = half;
    uint32_t sign = (h >> 15) & 1;
    uint32_t exp = (h >> 10) & 0x1F;
    uint32_t mant = h & 0x3FF;
    uint32_t f;
    if (exp == 0) {
        f = (sign << 31) | (mant << 13); // denormal
        return *reinterpret_cast<float*>(&f);
    }
    if (exp == 0x1F) {
        f = (sign << 31) | (0xFF << 23) | (mant << 13); // inf/nan
        return *reinterpret_cast<float*>(&f);
    }
    exp = exp - 15 + 127;
    mant = mant << 13;
    f = (sign << 31) | (exp << 23) | mant;
    return *reinterpret_cast<float*>(&f);
}

bool CaptureEngine::AcquireFrame(wgc::GraphicsCaptureItem& item, int rx, int ry, int cw, int ch,
    int mw, int mh, uint8_t*& outPixels, int& outWidth, int& outHeight)
{
    winrt::Windows::Graphics::SizeInt32 size;
    size.Width = mw;
    size.Height = mh;

    auto framePool = wgc::Direct3D11CaptureFramePool::Create(m_winrtDevice,
        wgd::DirectXPixelFormat::R16G16B16A16Float, 1, size);

    auto session = framePool.CreateCaptureSession(item);
    session.StartCapture();

    // Wait for one frame using message pump (avoids deadlock with COM callbacks)
    wgc::Direct3D11CaptureFrame frame{ nullptr };
    bool gotFrame = false;

    auto token = framePool.FrameArrived(
        [&](wgc::Direct3D11CaptureFramePool const&, auto&) {
            frame = framePool.TryGetNextFrame();
            gotFrame = true;
        });

    // Pump messages for up to 3 seconds
    DWORD startTime = GetTickCount();
    MSG msg;
    while (!gotFrame && (GetTickCount() - startTime < 3000)) {
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        Sleep(10);
    }
    framePool.FrameArrived(token);

    session.Close();
    framePool.Close();

    if (!gotFrame || !frame) {
        g_lastError = "Frame timeout after 3s";
        return false;
    }

    // Define interop interface inline
    struct __declspec(uuid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")) IDirect3DDxgiInterfaceAccess : IUnknown {
        virtual HRESULT STDMETHODCALLTYPE GetInterface(REFIID iid, void** p) = 0;
    };

    // Get native D3D11 texture from WinRT surface
    auto surface = frame.Surface();
    winrt::com_ptr<ID3D11Texture2D> frameTexture;
    {
        winrt::com_ptr<IDirect3DDxgiInterfaceAccess> access;
        surface.as(access);
        HRESULT hr2 = access->GetInterface(__uuidof(ID3D11Texture2D),
            reinterpret_cast<void**>(frameTexture.put()));
        if (FAILED(hr2)) { g_lastError = "GetInterface(D3D11Tex): 0x" + std::to_string(hr2); return false; }
    }

    // Create staging texture for HDR format
    D3D11_TEXTURE2D_DESC desc = {};
    desc.Width = mw;
    desc.Height = mh;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_R16G16B16A16_FLOAT;
    desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_STAGING;
    desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;

    winrt::com_ptr<ID3D11Texture2D> staging;
    HRESULT hr = m_d3dDevice->CreateTexture2D(&desc, nullptr, staging.put());
    if (FAILED(hr)) { g_lastError = "CreateStaging: 0x" + std::to_string(hr); return false; }

    m_d3dContext->CopyResource(staging.get(), frameTexture.get());

    // Map and read pixels
    D3D11_MAPPED_SUBRESOURCE mapped = {};
    hr = m_d3dContext->Map(staging.get(), 0, D3D11_MAP_READ, 0, &mapped);
    if (FAILED(hr)) { g_lastError = "Map: 0x" + std::to_string(hr); return false; }

    int stride = mapped.RowPitch;
    uint16_t* src = static_cast<uint16_t*>(mapped.pData);
    int srcStrideWords = stride / 2;

    outWidth = cw;
    outHeight = ch;
    outPixels = static_cast<uint8_t*>(CoTaskMemAlloc(cw * ch * 4));
    if (!outPixels) { m_d3dContext->Unmap(staging.get(), 0); return false; }

    for (int row = 0; row < ch; row++) {
        int srcOff = (ry + row) * srcStrideWords + rx * 4;
        int dstOff = row * cw * 4;
        for (int col = 0; col < cw; col++) {
            float r = HalfToFloat(src[srcOff + col * 4 + 0]);
            float g = HalfToFloat(src[srcOff + col * 4 + 1]);
            float b = HalfToFloat(src[srcOff + col * 4 + 2]);

            // HDR tone mapping: Reinhard
            r = r / (1.0f + r);
            g = g / (1.0f + g);
            b = b / (1.0f + b);

            // Linear to sRGB
            r = (r <= 0.0031308f) ? (12.92f * r) : (1.055f * powf(r, 1.0f / 2.4f) - 0.055f);
            g = (g <= 0.0031308f) ? (12.92f * g) : (1.055f * powf(g, 1.0f / 2.4f) - 0.055f);
            b = (b <= 0.0031308f) ? (12.92f * b) : (1.055f * powf(b, 1.0f / 2.4f) - 0.055f);

            outPixels[dstOff + col * 4 + 0] = static_cast<uint8_t>(std::clamp(b * 255.0f, 0.0f, 255.0f));
            outPixels[dstOff + col * 4 + 1] = static_cast<uint8_t>(std::clamp(g * 255.0f, 0.0f, 255.0f));
            outPixels[dstOff + col * 4 + 2] = static_cast<uint8_t>(std::clamp(r * 255.0f, 0.0f, 255.0f));
            outPixels[dstOff + col * 4 + 3] = 255;
        }
    }

    m_d3dContext->Unmap(staging.get(), 0);
    return true;
}

void CaptureEngine::Shutdown()
{
    m_monitors.clear();
    m_winrtDevice = nullptr;
    m_d3dContext = nullptr;
    m_d3dDevice = nullptr;
    m_initialized = false;
}

// C exports
bool __stdcall CaptureInitialize()
{
    if (g_engine) delete g_engine;
    g_engine = new CaptureEngine();
    return g_engine->Initialize();
}

bool __stdcall CaptureScreen(int x, int y, int w, int h, void* monitor, uint8_t** pixels, int* outW, int* outH)
{
    if (!g_engine || !pixels || !outW || !outH) return false;
    return g_engine->CaptureRegion(x, y, w, h, reinterpret_cast<HMONITOR>(monitor), *pixels, *outW, *outH);
}

void __stdcall CaptureFreePixels(uint8_t* pixels)
{
    if (pixels) CoTaskMemFree(pixels);
}

void __stdcall CaptureShutdown()
{
    if (g_engine) { delete g_engine; g_engine = nullptr; }
}

const char* __stdcall CaptureGetLastError()
{
    return g_lastError.c_str();
}
