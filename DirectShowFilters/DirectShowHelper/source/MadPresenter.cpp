// Copyright (C) 2005-2012 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#include "StdAfx.h"

#include <initguid.h>
#include <streams.h>
#include <d3dx9.h>

#include "madpresenter.h"
#include "dshowhelper.h"
#include "mvrInterfaces.h"

// For more details for memory leak detection see the alloctracing.h header
#include "..\..\alloctracing.h"
#include "StdString.h"
#include "../../mpc-hc_subs/src/dsutil/DSUtil.h"
#include <afxwin.h>
#include "../../MPAudioRenderer/source/SharedInclude.h"

const DWORD D3DFVF_VID_FRAME_VERTEX = D3DFVF_XYZRHW | D3DFVF_TEX1;

struct VID_FRAME_VERTEX
{
  float x;
  float y;
  float z;
  float rhw;
  float u;
  float v;
};

MPMadPresenter::MPMadPresenter(IVMR9Callback* pCallback, DWORD width, DWORD height, OAHWND parent, IDirect3DDevice9* pDevice, IMediaControl* pMediaControl) :
  CUnknown(NAME("MPMadPresenter"), NULL),
  m_pCallback(pCallback),
  m_dwGUIWidth(width),
  m_dwGUIHeight(height),
  m_hParent(parent),
  m_pDevice((IDirect3DDevice9Ex*)pDevice),
  m_pMediaControl(pMediaControl)
{
  Log("MPMadPresenter::Constructor() - instance 0x%x", this);
  m_pShutdown = false;
}

MPMadPresenter::~MPMadPresenter()
{
  if (m_pSRCB) {
    // nasty, but we have to let it know about our death somehow
    static_cast<CSubRenderCallback*>(static_cast<ISubRenderCallback*>(m_pSRCB))->SetDXRAP(nullptr);
  }

  if (m_pORCB) {
    // nasty, but we have to let it know about our death somehow
    static_cast<COsdRenderCallback*>(static_cast<IOsdRenderCallback*>(m_pORCB))->SetDXRAP(nullptr);
  }

  //// Unregister madVR Exclusive Callback
  //if (Com::SmartQIPtr<IMadVRExclusiveModeCallback> pEXL = m_pDXR)
  //  pEXL->Unregister(m_exclusiveCallback, this);

  MPMadPresenter::EnableExclusive(false);

  // Let's madVR restore original display mode (when adjust refresh it's handled by madVR)
  if (Com::SmartQIPtr<IMadVRCommand> pMadVrCmd = m_pMad)
  {
    pMadVrCmd->SendCommand("restoreDisplayModeNow");
    pMadVrCmd.Release();
  }

  if (Com::SmartQIPtr<IVideoWindow> pWindow = m_pMad)
  {
    pWindow->put_Owner(reinterpret_cast<OAHWND>(nullptr));
    pWindow->put_Visible(false);
    pWindow.Release();
  }

  m_pMad.FullRelease();
  m_pSRCB.FullRelease();
  m_pORCB.FullRelease();

  //g_renderManager.UnInit();
  //g_advancedSettings.m_guiAlgorithmDirtyRegions = m_kodiGuiDirtyAlgo;

  // the order is important here
  //CDSRendererCallback::Destroy();
  //SAFE_DELETE(m_pMadvrShared);
  //m_pSubPicQueue = nullptr;
  //m_pAllocator = nullptr;
  //m_pMad = nullptr;
  //m_pORCB = nullptr;
  //m_pSRCB = nullptr;

  Log("MPMadPresenter::Destructor() - instance 0x%x", this);
}

void MPMadPresenter::InitializeOSD()
{
  // IOsdRenderCallback
  Com::SmartQIPtr<IMadVROsdServices> pOR = m_pMad;
  if (!pOR) {
    m_pMad = nullptr;
    return;
  }

  m_pORCB = new COsdRenderCallback(this);
  if (FAILED(pOR->OsdSetRenderCallback("MP-GUI", m_pORCB))) {
    m_pMad = nullptr;
  }
}

void MPMadPresenter::SetOSDCallback()
{
  {
    CAutoLock cAutoLock(this);
    //InitializeOSD();
  }
}

IBaseFilter* MPMadPresenter::Initialize()
{
  CAutoLock cAutoLock(this);

  if (Com::SmartQIPtr<IBaseFilter> baseFilter = m_pMad)
    return baseFilter;

  return nullptr;
}

STDMETHODIMP MPMadPresenter::CreateRenderer(IUnknown** ppRenderer)
{
  CheckPointer(ppRenderer, E_POINTER);

  if (m_pMad) {
    return E_UNEXPECTED;
  }

  m_pMad.CoCreateInstance(CLSID_madVR, GetOwner());
  if (!m_pMad) {
    return E_FAIL;
  }

  Com::SmartQIPtr<ISubRender> pSR = m_pMad;
  if (!pSR) {
    m_pMad = nullptr;
    return E_FAIL;
  }

  m_pSRCB = new CSubRenderCallback(this);
  if (FAILED(pSR->SetCallback(m_pSRCB))) {
    m_pMad = nullptr;
    return E_FAIL;
  }

  // IOsdRenderCallback
  Com::SmartQIPtr<IMadVROsdServices> pOR = m_pMad;
  if (!pOR) {
    m_pMad = nullptr;
    return E_FAIL;
  }

  m_pORCB = new COsdRenderCallback(this);
  if (FAILED(pOR->OsdSetRenderCallback("MP-GUI", m_pORCB))) {
    m_pMad = nullptr;
    return E_FAIL;
  }

  // Configure initial Madvr Settings
  ConfigureMadvr();

  //CDSRendererCallback::Get()->Register(this);

  (*ppRenderer = reinterpret_cast<IUnknown*>(static_cast<INonDelegatingUnknown*>(this)))->AddRef();

  return S_OK;
}

void MPMadPresenter::EnableExclusive(bool bEnable)
{
  if (Com::SmartQIPtr<IMadVRCommand> pMadVrCmd = m_pMad)
    pMadVrCmd->SendCommandBool("disableExclusiveMode", !bEnable);
};

void MPMadPresenter::ConfigureMadvr()
{
  if (Com::SmartQIPtr<IMadVRCommand> pMadVrCmd = m_pMad)
    pMadVrCmd->SendCommandBool("disableSeekbar", true);

  if (Com::SmartQIPtr<IMadVRDirect3D9Manager> manager = m_pMad)
    manager->ConfigureDisplayModeChanger(false, true);

  // TODO implement IMadVRSubclassReplacement
  //if (Com::SmartQIPtr<IMadVRSubclassReplacement> pSubclassReplacement = m_pMad)  { }

  if (Com::SmartQIPtr<IVideoWindow> pWindow = m_pMad)
  {
    pWindow->SetWindowPosition(0, 0, m_dwGUIWidth, m_dwGUIHeight);
    pWindow->put_Owner(m_hParent);
  }

  // TODO - disable exclusive mode (need to read current setting)
  //if (Com::SmartQIPtr<IMadVRCommand> pMadVrCmd = m_pMad)
  //  pMadVrCmd->SendCommandBool("disableExclusiveMode", true);
}

HRESULT MPMadPresenter::Shutdown()
{
  { // Scope for autolock for the local variable (lock, which when deleted releases the lock)
    CAutoLock lock(this);

    Log("MPMadPresenter::Shutdown() scope start");

    m_pShutdown = true;

    if (m_pCallback)
    {
      m_pCallback->Release();
      m_pCallback = nullptr;
    }

    Log("MPMadPresenter::Shutdown() scope done ");
  } // Scope for autolock

  Log("MPMadPresenter::Shutdown()");

  return S_OK;
}

STDMETHODIMP MPMadPresenter::NonDelegatingQueryInterface(REFIID riid, void** ppv)
{
  if (riid != IID_IUnknown && m_pMad) {
    if (SUCCEEDED(m_pMad->QueryInterface(riid, ppv))) {
      return S_OK;
    }
  }

  return __super::NonDelegatingQueryInterface(riid, ppv);
}

HRESULT MPMadPresenter::ClearBackground(LPCSTR name, REFERENCE_TIME frameStart, RECT* fullOutputRect, RECT* activeVideoRect)
{
  HRESULT hr = E_UNEXPECTED;

  WORD videoHeight = (WORD)activeVideoRect->bottom - (WORD)activeVideoRect->top;
  WORD videoWidth = (WORD)activeVideoRect->right - (WORD)activeVideoRect->left;

  bool uiVisible = false;

  CAutoLock cAutoLock(this);

  if (!m_pMPTextureGui || !m_pMadGuiVertexBuffer || !m_pRenderTextureGui || !m_pCallback)
    return CALLBACK_EMPTY;

  m_dwHeight = (WORD)fullOutputRect->bottom - (WORD)fullOutputRect->top;
  m_dwWidth = (WORD)fullOutputRect->right - (WORD)fullOutputRect->left;

  if (FAILED(hr = RenderToTexture(m_pMPTextureGui)))
    return hr;

  if (FAILED(hr = m_deviceState.Store()))
    return hr;

  if (FAILED(hr = m_pCallback->RenderGui(videoWidth, videoHeight, videoWidth, videoHeight)))
    return hr;

  uiVisible = hr == S_OK ? true : false;

  if (FAILED(hr = m_pDevice->PresentEx(nullptr, nullptr, nullptr, nullptr, D3DPRESENT_FORCEIMMEDIATE)))
    return hr;

  if (FAILED(hr = SetupMadDeviceState()))
    return hr;

  if (FAILED(hr = SetupOSDVertex(m_pMadGuiVertexBuffer)))
    return hr;

  // Draw MP texture on madVR device's side
  if (FAILED(hr = RenderTexture(m_pMadGuiVertexBuffer, m_pRenderTextureGui)))
    return hr;

  if (FAILED(hr = m_deviceState.Restore()))
    return hr;

  return uiVisible ? CALLBACK_USER_INTERFACE : CALLBACK_EMPTY;
}

HRESULT MPMadPresenter::RenderOsd(LPCSTR name, REFERENCE_TIME frameStart, RECT* fullOutputRect, RECT* activeVideoRect)
{
  HRESULT hr = E_UNEXPECTED;

  WORD videoHeight = (WORD)activeVideoRect->bottom - (WORD)activeVideoRect->top;
  WORD videoWidth = (WORD)activeVideoRect->right - (WORD)activeVideoRect->left;

  bool uiVisible = false;

  CAutoLock cAutoLock(this);

  if (!m_pMPTextureOsd || !m_pMadOsdVertexBuffer || !m_pRenderTextureOsd || !m_pCallback)
    return CALLBACK_EMPTY;

  IDirect3DSurface9* SurfaceMadVr = nullptr; // This will be released by C# side

  m_dwHeight = (WORD)fullOutputRect->bottom - (WORD)fullOutputRect->top;
  m_dwWidth = (WORD)fullOutputRect->right - (WORD)fullOutputRect->left;

  // Handle GetBackBuffer to be done only 2 frames
  countFrame++;
  if (countFrame == firstFrame || countFrame == secondFrame)
  {
    if (SUCCEEDED(hr = m_pMadD3DDev->GetBackBuffer(0, 0, D3DBACKBUFFER_TYPE_MONO, &SurfaceMadVr)))
    {
      if (SUCCEEDED(hr = m_pCallback->RenderFrame(videoWidth, videoHeight, videoWidth, videoHeight, reinterpret_cast<DWORD>(SurfaceMadVr))))
      {
        SurfaceMadVr->Release();
      }
      if (countFrame == secondFrame)
      {
        countFrame = resetFrame;
      }
    }
  }

  if (FAILED(hr = RenderToTexture(m_pMPTextureOsd)))
    return hr;

  if (FAILED(hr = m_deviceState.Store()))
    return hr;

  if (FAILED(hr = m_pCallback->RenderOverlay(videoWidth, videoHeight, videoWidth, videoHeight)))
    return hr;

  uiVisible = hr == S_OK ? true : false;

  if (FAILED(hr = m_pDevice->PresentEx(nullptr, nullptr, nullptr, nullptr, D3DPRESENT_FORCEIMMEDIATE)))
    return hr;

  if (FAILED(hr = SetupMadDeviceState()))
    return hr;

  if (FAILED(hr = SetupOSDVertex(m_pMadOsdVertexBuffer)))
    return hr;

  // Draw MP texture on madVR device's side
  if (FAILED(hr = RenderTexture(m_pMadOsdVertexBuffer, m_pRenderTextureOsd)))
    return hr;

  if (FAILED(hr = m_deviceState.Restore()))
    return hr;

  return uiVisible ? CALLBACK_USER_INTERFACE : CALLBACK_EMPTY;
}

HRESULT MPMadPresenter::RenderToTexture(IDirect3DTexture9* pTexture)
{
  HRESULT hr = E_UNEXPECTED;
  IDirect3DSurface9* pSurface = nullptr; // This will be relased by C# side

  if (FAILED(hr = pTexture->GetSurfaceLevel(0, &pSurface)))
    return hr;

  if (FAILED(hr = m_pCallback->SetRenderTarget((DWORD)pSurface)))
    return hr;

  if (FAILED(hr = m_pDevice->Clear(0, NULL, D3DCLEAR_TARGET, D3DXCOLOR(0, 0, 0, 0), 1.0f, 0)))
    return hr;

  return hr;
}

HRESULT MPMadPresenter::RenderTexture(IDirect3DVertexBuffer9* pVertexBuf, IDirect3DTexture9* pTexture)
{
  if (!m_pMadD3DDev)
    return S_OK;

  HRESULT hr = E_UNEXPECTED;

  if (SUCCEEDED(hr = m_pMadD3DDev->SetStreamSource(0, pVertexBuf, 0, sizeof(VID_FRAME_VERTEX))))
  {
    //Log("RenderTexture SetStreamSource hr: 0x%08x", hr);
    if (SUCCEEDED(hr = m_pMadD3DDev->SetTexture(0, pTexture)))
    {
      //Log("RenderTexture SetTexture hr: 0x%08x", hr);
      hr = m_pMadD3DDev->DrawPrimitive(D3DPT_TRIANGLEFAN, 0, 2);
      //Log("RenderTexture DrawPrimitive hr: 0x%08x", hr);
    }
  }

  return S_OK;
  //Log("RenderTexture hr: 0x%08x", hr);
}

HRESULT MPMadPresenter::SetupOSDVertex(IDirect3DVertexBuffer9* pVertextBuf)
{
  VID_FRAME_VERTEX* vertices = nullptr;

  // Lock the vertex buffer
  HRESULT hr = pVertextBuf->Lock(0, 0, (void**)&vertices, D3DLOCK_DISCARD);

  if (SUCCEEDED(hr))
  {
    RECT rDest;
    rDest.bottom = m_dwHeight;
    rDest.left = 0;
    rDest.right = m_dwWidth;
    rDest.top = 0;

    vertices[0].x = (float)rDest.left - 0.5f;
    vertices[0].y = (float)rDest.top - 0.5f;
    vertices[0].z = 0.0f;
    vertices[0].rhw = 1.0f;
    vertices[0].u = 0.0f;
    vertices[0].v = 0.0f;

    vertices[1].x = (float)rDest.right - 0.5f;
    vertices[1].y = (float)rDest.top - 0.5f;
    vertices[1].z = 0.0f;
    vertices[1].rhw = 1.0f;
    vertices[1].u = 1.0f;
    vertices[1].v = 0.0f;

    vertices[2].x = (float)rDest.right - 0.5f;
    vertices[2].y = (float)rDest.bottom - 0.5f;
    vertices[2].z = 0.0f;
    vertices[2].rhw = 1.0f;
    vertices[2].u = 1.0f;
    vertices[2].v = 1.0f;

    vertices[3].x = (float)rDest.left - 0.5f;
    vertices[3].y = (float)rDest.bottom - 0.5f;
    vertices[3].z = 0.0f;
    vertices[3].rhw = 1.0f;
    vertices[3].u = 0.0f;
    vertices[3].v = 1.0f;

    hr = pVertextBuf->Unlock();
    if (FAILED(hr))
      return hr;
  }

  return hr;
}

HRESULT MPMadPresenter::SetupMadDeviceState()
{
  HRESULT hr = E_UNEXPECTED;

  RECT newScissorRect;
  newScissorRect.bottom = m_dwHeight;
  newScissorRect.top = 0;
  newScissorRect.left = 0;
  newScissorRect.right = m_dwWidth;

  if (FAILED(hr = m_pMadD3DDev->SetScissorRect(&newScissorRect)))
    return hr;

  if (FAILED(hr = m_pMadD3DDev->SetVertexShader(NULL)))
    return hr;

  if (FAILED(hr = m_pMadD3DDev->SetFVF(D3DFVF_VID_FRAME_VERTEX)))
    return hr;

  if (FAILED(hr = m_pMadD3DDev->SetPixelShader(NULL)))
    return hr;

  if (FAILED(hr = m_pMadD3DDev->SetRenderState(D3DRS_ALPHABLENDENABLE, TRUE)))
    return hr;

  if (FAILED(hr = m_pMadD3DDev->SetRenderState(D3DRS_CULLMODE, D3DCULL_NONE)))
    return hr;

  if (FAILED(hr = m_pMadD3DDev->SetRenderState(D3DRS_LIGHTING, FALSE)))
    return hr;

  if (FAILED(hr = m_pMadD3DDev->SetRenderState(D3DRS_ZENABLE, FALSE)))
    return hr;

  if (FAILED(hr = m_pMadD3DDev->SetRenderState(D3DRS_SRCBLEND, D3DBLEND_ONE)))
    return hr;

  if (FAILED(hr = m_pMadD3DDev->SetRenderState(D3DRS_DESTBLEND, D3DBLEND_INVSRCALPHA)))
    return hr;

  return hr;
}

HRESULT MPMadPresenter::SetDeviceOsd(IDirect3DDevice9* pD3DDev)
{
  CAutoLock cAutoLock(this);
  if (!pD3DDev)
  {
    // release all resources
    //m_pSubPicQueue = nullptr;
    //m_pAllocator = nullptr;
  }
  return S_OK;
}

HRESULT MPMadPresenter::SetDevice(IDirect3DDevice9* pD3DDev)
{
  HRESULT hr = S_FALSE;

  CAutoLock cAutoLock(this);

  Log("MPMadPresenter::SetDevice() device 0x:%x", pD3DDev);

  m_pMadD3DDev = (IDirect3DDevice9Ex*)pD3DDev;

  if (m_pMadD3DDev)
  {
    m_deviceState.SetDevice(m_pMadD3DDev);

    if (FAILED(hr = m_pMadD3DDev->CreateVertexBuffer(sizeof(VID_FRAME_VERTEX) * 4, D3DUSAGE_WRITEONLY, D3DFVF_VID_FRAME_VERTEX, D3DPOOL_DEFAULT, &m_pMadGuiVertexBuffer.p, NULL)))
      return hr;

    if (FAILED(hr = m_pMadD3DDev->CreateVertexBuffer(sizeof(VID_FRAME_VERTEX) * 4, D3DUSAGE_WRITEONLY, D3DFVF_VID_FRAME_VERTEX, D3DPOOL_DEFAULT, &m_pMadOsdVertexBuffer.p, NULL)))
      return hr;

    if (FAILED(hr = m_pDevice->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pMPTextureGui.p, &m_hSharedGuiHandle)))
      return hr;

    if (FAILED(hr = m_pMadD3DDev->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pRenderTextureGui.p, &m_hSharedGuiHandle)))
      return hr;

    if (FAILED(hr = m_pDevice->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pMPTextureOsd.p, &m_hSharedOsdHandle)))
      return hr;

    if (FAILED(hr = m_pMadD3DDev->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pRenderTextureOsd.p, &m_hSharedOsdHandle)))
      return hr;

    if (m_pCallback)
    {
      m_pInitOSDRender = false;
      m_pCallback->SetSubtitleDevice((DWORD)m_pMadD3DDev);
      Log("MPMadPresenter::SetDevice() SetSubtitleDevice for D3D : 0x:%x", m_pMadD3DDev);
    }
  }
  else
    m_pMadD3DDev = nullptr;

  return hr;
}

HRESULT MPMadPresenter::Render(REFERENCE_TIME frameStart, int left, int top, int right, int bottom, int width, int height)
{
  if (m_pCallback)
  {
    CAutoLock cAutoLock(this);

    if (!m_pInitOSDRender)
    {
      m_pInitOSDRender = true;
      m_pCallback->ForceOsdUpdate(true);
      Log("MPMadPresenter::Render() ForceOsdUpdate");
    }
    m_deviceState.Store();
    SetupMadDeviceState();

    m_pCallback->RenderSubtitle(frameStart, left, top, right, bottom, width, height);

    m_deviceState.Restore();
  }

  return S_OK;
}
