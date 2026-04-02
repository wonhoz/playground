● 각 모델의 공식 출처와 ONNX 변환 방법입니다.

  ---
  모델 다운로드 방법

  1. RealESRGAN x4plus / Anime (가장 간단)

  공식 GitHub에서 ONNX 파일 직접 제공합니다.

  https://github.com/xinntao/Real-ESRGAN

  다운로드 직링크 (GitHub Releases):
  # RealESRGAN x4plus (실사 사진용)
  https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip

  단, 위 파일은 .bin/.param 형식(ncnn)이라 ONNX가 아닙니다.

  ONNX 버전은 별도 변환 레포에서 받아야 합니다:

  https://github.com/Cyronkv/real-esrgan-onnx

  또는 Hugging Face에서 직접:
  # RealESRGAN x4plus
  https://huggingface.co/ssube/stable-diffusion-x4-upscaler-onnx

  # 더 간단한 경로:
  https://huggingface.co/ai-forever/Real-ESRGAN

  ---
  2. waifu2x-cunet (ONNX 직접 제공)

  https://github.com/nihui/waifu2x-ncnn-vulkan

  ONNX 버전:
  https://github.com/AaronFeng753/Waifu2x-Extension-GUI

  ---
  현실적인 가장 빠른 방법

  Hugging Face에서 검색하면 커뮤니티가 변환한 ONNX가 많습니다:

  https://huggingface.co/models?search=realesrgan+onnx
  https://huggingface.co/models?search=waifu2x+onnx

  대표 레포:
  https://github.com/Sanster/iopaint  ← 내부에 ONNX 모델 포함
  https://github.com/chaiNNer-org/chaiNNer  ← 모델 다운로드 관리자 포함

  ---
  솔직히 말씀드리면

  ONNX upscaler 모델은 단일 공식 배포처가 없고 커뮤니티가 PyTorch → ONNX로 변환해서 올리는 구조입니다.

  앱에 모델 자동 다운로드 기능을 추가하는 게 훨씬 편합니다. 제가 구현해 드릴까요?

  앱 실행 → 모델 선택 → "다운로드" 버튼 클릭
  → Hugging Face / GitHub Releases에서 자동 다운로드 + 진행률 표시
  → %LocalAppData%\Playground\Brush.Scale\models\ 에 자동 저장

  이렇게 하면 사용자가 직접 파일을 찾아서 넣을 필요가 없습니다.