import { Component } from '@angular/core';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-upload',
  templateUrl: './upload.component.html'
})
export class UploadComponent {
  file?: File;
  metadata: any;
  uploading = false;
  constructor(private api: ApiService){}

  onFileSelected(e: any){
    this.file = e.target.files[0];
  }

  async upload(){
    if(!this.file) return;
    this.uploading = true;
    this.api.uploadFile(this.file).subscribe({
      next: (meta) => { this.metadata = meta; this.uploading=false; },
      error: (err) => { alert('Upload failed: '+ JSON.stringify(err)); this.uploading=false; }
    });
  }
}
